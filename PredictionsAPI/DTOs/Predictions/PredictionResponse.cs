namespace PredictionsAPI.DTOs.Predictions;

public class PredictionResponse
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public string HomeTeam { get; set; } = string.Empty;

    public string AwayTeam { get; set; } = string.Empty;

    public int HomeGoals { get; set; }

    public int AwayGoals { get; set; }

    public string UserDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
