namespace PredictionsAPI.DTOs.Games;

public class GameResponse
{
    public int Id { get; set; }

    public int TournamentId { get; set; }

    public string HomeTeam { get; set; } = string.Empty;

    public string AwayTeam { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public int? HomeGoals { get; set; }

    public int? AwayGoals { get; set; }

    public bool IsFinished { get; set; }

    public string? HomeCrestUrl { get; set; }

    public string? AwayCrestUrl { get; set; }
}
