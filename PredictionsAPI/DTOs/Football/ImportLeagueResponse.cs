using PredictionsAPI.DTOs.Tournaments;

namespace PredictionsAPI.DTOs.Football;

public class ImportLeagueResponse
{
    public TournamentResponse Tournament { get; set; } = null!;
    public int GamesImported { get; set; }
}
