namespace PredictionsAPI.DTOs.Games;

public class SyncGameScoreRequest
{
    public int? HomeGoals { get; set; }

    public int? AwayGoals { get; set; }

    public bool IsFinished { get; set; }

    public int? FifaMatchStatus { get; set; }

    public string? FifaMatchTime { get; set; }
}
