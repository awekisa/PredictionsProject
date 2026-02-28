namespace PredictionsAPI.Entities;

public class Tournament
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? ExternalLeagueId { get; set; }

    public int? ExternalSeason { get; set; }

    public ICollection<Game> Games { get; set; } = new List<Game>();
}
