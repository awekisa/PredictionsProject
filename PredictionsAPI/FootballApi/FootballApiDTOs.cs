namespace PredictionsAPI.FootballApi;

// GET /v4/competitions
public class FootballDataCompetitionsResponse
{
    public List<FootballDataCompetitionDto> Competitions { get; set; } = new();
}

public class FootballDataCompetitionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Emblem { get; set; }
    public FootballDataAreaDto Area { get; set; } = new();
    public FootballDataSeasonDto? CurrentSeason { get; set; }
}

public class FootballDataAreaDto
{
    public string Name { get; set; } = string.Empty;
}

public class FootballDataSeasonDto
{
    public string StartDate { get; set; } = string.Empty;
}

// GET /v4/competitions/{id}/matches
public class FootballDataMatchesResponse
{
    public List<FootballDataMatchDto> Matches { get; set; } = new();
}

public class FootballDataMatchDto
{
    public int Id { get; set; }
    public DateTime UtcDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public FootballDataTeamDto HomeTeam { get; set; } = new();
    public FootballDataTeamDto AwayTeam { get; set; } = new();
    public FootballDataScoreDto Score { get; set; } = new();
}

public class FootballDataTeamDto
{
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Crest { get; set; }
}

public class FootballDataScoreDto
{
    public FootballDataGoalsDto FullTime { get; set; } = new();
}

public class FootballDataGoalsDto
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}

// GET /v4/competitions/{id}/standings
public class FootballDataStandingsResponse
{
    public List<FootballDataStandingDto> Standings { get; set; } = new();
}

public class FootballDataStandingDto
{
    public string Stage { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Group { get; set; }
    public List<FootballDataStandingRowDto> Table { get; set; } = new();
}

public class FootballDataStandingRowDto
{
    public int Position { get; set; }
    public FootballDataStandingTeamDto Team { get; set; } = new();
    public int PlayedGames { get; set; }
    public int Won { get; set; }
    public int Draw { get; set; }
    public int Lost { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Points { get; set; }
}

public class FootballDataStandingTeamDto
{
    public string Name { get; set; } = string.Empty;
    public string? Crest { get; set; }
}
