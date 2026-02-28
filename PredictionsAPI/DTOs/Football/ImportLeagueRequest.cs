namespace PredictionsAPI.DTOs.Football;

public class ImportLeagueRequest
{
    public int LeagueId { get; set; }
    public int Season { get; set; }
    public string Name { get; set; } = string.Empty;
}
