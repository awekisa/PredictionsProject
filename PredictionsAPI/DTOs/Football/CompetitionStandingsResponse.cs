namespace PredictionsAPI.DTOs.Football;

public class CompetitionStandingsResponse
{
    public List<StandingGroupResponse> Groups { get; set; } = new();
}

public class StandingGroupResponse
{
    public string Stage { get; set; } = string.Empty;
    public string? Group { get; set; }
    public List<StandingRowResponse> Table { get; set; } = new();
}

public class StandingRowResponse
{
    public int Position { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public string? TeamCrest { get; set; }
    public int PlayedGames { get; set; }
    public int Won { get; set; }
    public int Draw { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
}
