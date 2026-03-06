namespace PredictionsAPI.DTOs.Standings;

public class UserPredictionDetailResponse
{
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public string? HomeCrestUrl { get; set; }
    public string? AwayCrestUrl { get; set; }
    public string? HomeTeamShort { get; set; }
    public string? AwayTeamShort { get; set; }
    public int PredictedHome { get; set; }
    public int PredictedAway { get; set; }
    public int ActualHome { get; set; }
    public int ActualAway { get; set; }
    public int PointsEarned { get; set; }
    public DateTime MatchDate { get; set; }
}
