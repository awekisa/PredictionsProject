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

    public async Task<List<FootballDataCompetitionDto>> GetCompetitionsAsync()
    {
        _logger.LogInformation("football-data.org: fetching available competitions");

        var response = await _http.GetAsync("competitions");
        CaptureRateLimitHeaders(response);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var msg = TryExtractMessage(content) ?? content;
            _logger.LogError("football-data.org GetCompetitions failed: HTTP {StatusCode} – {Message}",
                (int)response.StatusCode, msg);
            throw new InvalidOperationException($"football-data.org error: {msg}");
        }

        var wrapper = JsonSerializer.Deserialize<FootballDataCompetitionsResponse>(content, _jsonOptions);
        var results = wrapper?.Competitions ?? [];

        _logger.LogInformation("football-data.org: GetCompetitions returned {Count} competition(s)", results.Count);
        return results;
    }

    public async Task<FootballDataCompetitionDto?> GetCompetitionAsync(int competitionId)
    {
        _logger.LogInformation("football-data.org: fetching competition {CompetitionId}", competitionId);

        var response = await _http.GetAsync($"competitions/{competitionId}");
        CaptureRateLimitHeaders(response);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var msg = TryExtractMessage(content) ?? content;
            _logger.LogWarning("football-data.org GetCompetition failed: HTTP {StatusCode} – {Message}",
                (int)response.StatusCode, msg);
            return null;
        }

        return JsonSerializer.Deserialize<FootballDataCompetitionDto>(content, _jsonOptions);
    }

    public async Task<List<FootballDataMatchDto>> GetMatchesAsync(int competitionId, int season)
    {
        _logger.LogInformation("football-data.org: fetching matches for competition {CompetitionId} season {Season}",
            competitionId, season);

        var response = await _http.GetAsync($"competitions/{competitionId}/matches?season={season}");
        CaptureRateLimitHeaders(response);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var msg = TryExtractMessage(content) ?? content;
            _logger.LogError("football-data.org GetMatches failed: HTTP {StatusCode} – {Message}",
                (int)response.StatusCode, msg);
            throw new InvalidOperationException($"football-data.org error: {msg}");
        }

        var wrapper = JsonSerializer.Deserialize<FootballDataMatchesResponse>(content, _jsonOptions);
        var results = wrapper?.Matches ?? [];

        if (results.Count == 0)
            _logger.LogWarning(
                "football-data.org GetMatches returned 0 matches for competition {CompetitionId} season {Season}. Raw: {Body}",
                competitionId, season, content);
        else
            _logger.LogInformation("football-data.org: GetMatches returned {Count} match(es)", results.Count);

        return results;
    }

    public async Task<List<FootballDataStandingDto>> GetStandingsAsync(int competitionId, int season)
    {
        _logger.LogInformation("football-data.org: fetching standings for competition {CompetitionId} season {Season}",
            competitionId, season);

        var response = await _http.GetAsync($"competitions/{competitionId}/standings?season={season}");
        CaptureRateLimitHeaders(response);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var msg = TryExtractMessage(content) ?? content;
            _logger.LogWarning("football-data.org GetStandings failed: HTTP {StatusCode} – {Message}",
                (int)response.StatusCode, msg);
            return [];
        }

        var wrapper = JsonSerializer.Deserialize<FootballDataStandingsResponse>(content, _jsonOptions);
        var all = wrapper?.Standings ?? [];

        _logger.LogInformation(
            "football-data.org GetStandings: {Total} standing group(s) returned (stages: {Stages})",
            all.Count,
            string.Join(", ", all.Select(s => $"{s.Stage}/{s.Type}/{s.Group ?? "null"}")));

        // Only return TOTAL type (excludes HOME/AWAY splits)
        var total = all.Where(s => s.Type == "TOTAL").ToList();

        if (total.Count == 0 && all.Count > 0)
            _logger.LogWarning(
                "football-data.org GetStandings: no TOTAL entries found. Types present: {Types}. Raw: {Body}",
                string.Join(", ", all.Select(s => s.Type).Distinct()),
                content[..Math.Min(500, content.Length)]);
        else if (total.Count == 0)
            _logger.LogWarning(
                "football-data.org GetStandings: 0 standings returned for competition {CompetitionId} season {Season}. Raw: {Body}",
                competitionId, season, content[..Math.Min(500, content.Length)]);

        return total;
    }

    private static string? TryExtractMessage(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { }
        return null;
    }

    private void CaptureRateLimitHeaders(HttpResponseMessage response)
    {
        // football-data.org free plan: 10 requests/minute
        if (response.Headers.TryGetValues("X-Requests-Available-Minute", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var available))
        {
            _statusStore.Update(10, available);

            if (available <= 2)
                _logger.LogWarning(
                    "football-data.org rate limit low: {Available}/10 requests remaining this minute", available);
            else
                _logger.LogInformation(
                    "football-data.org rate limit: {Available}/10 requests remaining this minute", available);
        }
    }
}
