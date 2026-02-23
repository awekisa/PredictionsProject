namespace PredictionsAPI.Entities;

public class Game
{
    public int Id { get; set; }

    public int TournamentId { get; set; }

    public string HomeTeam { get; set; } = string.Empty;

    public string AwayTeam { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public int? HomeGoals { get; set; }

    public int? AwayGoals { get; set; }

    public Tournament Tournament { get; set; } = null!;

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
