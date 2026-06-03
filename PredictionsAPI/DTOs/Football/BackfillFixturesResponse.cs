namespace PredictionsAPI.DTOs.Football;

public class BackfillFixturesResponse
{
    public int ProviderFixtures { get; set; }
    public int ExistingGames { get; set; }
    public int Added { get; set; }
    public int MatchedExisting { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedUndetermined { get; set; }
}
