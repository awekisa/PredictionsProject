namespace PredictionsAPI.DTOs.Tournaments;

public class TournamentResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public int? ExternalLeagueId { get; set; }

    public int? ExternalSeason { get; set; }

    public string? EmblemUrl { get; set; }
}
