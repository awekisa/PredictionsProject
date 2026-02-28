namespace PredictionsAPI.FootballApi;

public class FootballApiWrapper<T>
{
    public List<T> Response { get; set; } = new();
}

public class FootballLeagueDto
{
    public FootballLeagueInfo League { get; set; } = null!;
    public FootballCountryInfo Country { get; set; } = null!;
    public List<FootballSeasonInfo> Seasons { get; set; } = new();
}

public class FootballLeagueInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Logo { get; set; }
}

public class FootballCountryInfo
{
    public string Name { get; set; } = string.Empty;
}

public class FootballSeasonInfo
{
    public int Year { get; set; }
    public bool Current { get; set; }
}

public class FootballFixtureDto
{
    public FootballFixtureInfo Fixture { get; set; } = null!;
    public FootballTeamsInfo Teams { get; set; } = null!;
    public FootballGoalsInfo Goals { get; set; } = null!;
}

public class FootballFixtureInfo
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public FootballStatusInfo Status { get; set; } = null!;
}

public class FootballStatusInfo
{
    public string Short { get; set; } = string.Empty;
}

public class FootballTeamsInfo
{
    public FootballTeamInfo Home { get; set; } = null!;
    public FootballTeamInfo Away { get; set; } = null!;
}

public class FootballTeamInfo
{
    public string Name { get; set; } = string.Empty;
}

public class FootballGoalsInfo
{
    public int? Home { get; set; }
    public int? Away { get; set; }
}
