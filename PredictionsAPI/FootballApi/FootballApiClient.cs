using System.Text.Json;

namespace PredictionsAPI.FootballApi;

public class FootballApiClient
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FootballApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<FootballLeagueDto>> SearchLeaguesAsync(string query)
    {
        var response = await _http.GetAsync($"leagues?search={Uri.EscapeDataString(query)}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var wrapper = JsonSerializer.Deserialize<FootballApiWrapper<FootballLeagueDto>>(content, _jsonOptions);
        return wrapper?.Response ?? new List<FootballLeagueDto>();
    }

    public async Task<List<FootballFixtureDto>> GetFixturesAsync(int leagueId, int season)
    {
        var response = await _http.GetAsync($"fixtures?league={leagueId}&season={season}");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var wrapper = JsonSerializer.Deserialize<FootballApiWrapper<FootballFixtureDto>>(content, _jsonOptions);
        return wrapper?.Response ?? new List<FootballFixtureDto>();
    }
}
