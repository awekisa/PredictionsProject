using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PredictionsAPI.FootballApi;

public class FootballApiClient
{
    private readonly HttpClient _http;
    private readonly FootballApiStatusStore _statusStore;
    private readonly ILogger<FootballApiClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FootballApiClient(HttpClient http, FootballApiStatusStore statusStore, ILogger<FootballApiClient> logger)
    {
        _http = http;
        _statusStore = statusStore;
        _logger = logger;
    }

    public async Task<List<FootballLeagueDto>> SearchLeaguesAsync(string query)
    {
        _logger.LogInformation("API-Football: searching leagues for '{Query}'", query);

        var response = await _http.GetAsync($"leagues?search={Uri.EscapeDataString(query)}");
        CaptureRateLimitHeaders(response);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("API-Football SearchLeagues failed: HTTP {StatusCode} – {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync();
        var wrapper = JsonSerializer.Deserialize<FootballApiWrapper<FootballLeagueDto>>(content, _jsonOptions);
        var results = wrapper?.Response ?? [];

        _logger.LogInformation("API-Football: SearchLeagues returned {Count} league(s)", results.Count);
        return results;
    }

    public async Task<List<FootballFixtureDto>> GetFixturesAsync(int leagueId, int season)
    {
        _logger.LogInformation("API-Football: fetching fixtures for league {LeagueId} season {Season}",
            leagueId, season);

        var response = await _http.GetAsync($"fixtures?league={leagueId}&season={season}");
        CaptureRateLimitHeaders(response);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError("API-Football GetFixtures failed: HTTP {StatusCode} – {Body}",
                (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var content = await response.Content.ReadAsStringAsync();
        var wrapper = JsonSerializer.Deserialize<FootballApiWrapper<FootballFixtureDto>>(content, _jsonOptions);
        var results = wrapper?.Response ?? [];

        _logger.LogInformation("API-Football: GetFixtures returned {Count} fixture(s)", results.Count);
        return results;
    }

    private void CaptureRateLimitHeaders(HttpResponseMessage response)
    {
        int? limit = null;
        int? remaining = null;

        if (response.Headers.TryGetValues("x-ratelimit-requests-limit", out var limitValues) &&
            int.TryParse(limitValues.FirstOrDefault(), out var l))
        {
            limit = l;
        }

        if (response.Headers.TryGetValues("x-ratelimit-requests-remaining", out var remainingValues) &&
            int.TryParse(remainingValues.FirstOrDefault(), out var r))
        {
            remaining = r;
        }

        if (limit.HasValue && remaining.HasValue)
        {
            _statusStore.Update(limit, remaining);

            if (remaining.Value <= 10)
            {
                _logger.LogWarning(
                    "API-Football rate limit critically low: {Remaining}/{Limit} daily requests remaining",
                    remaining.Value, limit.Value);
            }
            else
            {
                _logger.LogInformation("API-Football rate limit: {Remaining}/{Limit} daily requests remaining",
                    remaining.Value, limit.Value);
            }
        }
    }
}
