namespace PredictionsAPI.DTOs.Football;

public class LeagueSearchResult
{
    public int LeagueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Logo { get; set; }
    public List<int> Seasons { get; set; } = new();
}
