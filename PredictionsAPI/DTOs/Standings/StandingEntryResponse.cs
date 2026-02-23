namespace PredictionsAPI.DTOs.Standings;

public class StandingEntryResponse
{
    public int Position { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public int Points { get; set; }

    public int CorrectScores { get; set; }

    public int TotalPredictions { get; set; }
}
