using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PredictionsAPI.Entities;

namespace PredictionsAPI.OAuth;

public sealed class ClientMetadata
{
    [JsonPropertyName("client_id")] public string? ClientId { get; set; }
    [JsonPropertyName("client_name")] public string? ClientName { get; set; }
    [JsonPropertyName("redirect_uris")] public string[]? RedirectUris { get; set; }
    [JsonPropertyName("token_endpoint_auth_method")] public string? AuthMethod { get; set; }
    [JsonPropertyName("grant_types")] public string[]? GrantTypes { get; set; }
    [JsonPropertyName("response_types")] public string[]? ResponseTypes { get; set; }
}

public interface IClientMetadataResolver
{
    Task<ClientMetadata> ResolveAsync(string clientId, CancellationToken ct);
}

// Fetch only public HTTPS documents, pin the socket to a checked address, and never follow redirects.
public sealed class ClientMetadataResolver : IClientMetadataResolver
{
    public async Task<ClientMetadata> ResolveAsync(string clientId, CancellationToken ct)
    {
        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Port != 443 || uri.AbsolutePath == "/" || uri.UserInfo.Length > 0 || uri.Fragment.Length > 0)
            throw new OAuthException("invalid_client_metadata");
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, UseCookies = false,
            ConnectCallback = async (context, token) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
                if (addresses.Length == 0 || addresses.Any(a => !IsPublicAddress(a)))
                    throw new HttpRequestException("Non-public metadata address.");
                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(addresses[0], 443), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 65536 ||
                response.Content.Headers.ContentType?.MediaType != "application/json")
                throw new OAuthException("invalid_client_metadata");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (body.Length + read > 65536) throw new OAuthException("invalid_client_metadata");
                body.Write(buffer, 0, read);
            }
            var result = JsonSerializer.Deserialize<ClientMetadata>(body.ToArray());
            if (result?.ClientId != clientId) throw new OAuthException("invalid_client_metadata");
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        { throw new OAuthException("invalid_client_metadata"); }
    }

    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        var b = address.GetAddressBytes();
        if (b.Length == 16)
            // Only global unicast; exclude documentation, Teredo and 6to4 translation ranges.
            return (b[0] & 0xe0) == 0x20 && !(b[0] == 0x20 && b[1] == 0x02) &&
                !(b[0] == 0x20 && b[1] == 0x01 && (b[2] < 2 || (b[2] == 0x0d && b[3] == 0xb8)));
        return b[0] != 0 && b[0] != 10 && b[0] != 127 && b[0] < 224 &&
            !(b[0] == 100 && b[1] is >= 64 and <= 127) && !(b[0] == 169 && b[1] == 254) &&
            !(b[0] == 172 && b[1] is >= 16 and <= 31) && !(b[0] == 192 && (b[1] == 168 || b[1] == 0 || b[1] == 2)) &&
            !(b[0] == 198 && (b[1] is 18 or 19 || (b[1] == 51 && b[2] == 100))) &&
            !(b[0] == 203 && b[1] == 0 && b[2] == 113);
    }
}

public sealed class OAuthException(string error) : Exception(error)
{
    public string Error => Message;
}
