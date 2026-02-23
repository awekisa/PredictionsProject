namespace PredictionsAPI.Entities;

public class Prediction
{
    public int Id { get; set; }

    public int GameId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public int HomeGoals { get; set; }

    public int AwayGoals { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Game Game { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
