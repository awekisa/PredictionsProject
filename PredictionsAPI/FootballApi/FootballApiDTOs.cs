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
