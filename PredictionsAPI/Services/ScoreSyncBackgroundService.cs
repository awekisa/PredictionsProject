using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;
using PredictionsAPI.Services.Interfaces;

namespace PredictionsAPI.Services;

public class ScoreSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScoreSyncBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);

    public ScoreSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ScoreSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_interval, stoppingToken);
            await SyncScoresAsync(stoppingToken);
        }
    }

    private async Task SyncScoresAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var syncService = scope.ServiceProvider.GetRequiredService<IFootballSyncService>();

            var now = DateTime.UtcNow;

            // Only sync tournaments that have started-but-unfinished games with an external ID
            var tournamentIds = await db.Tournaments
                .Where(t => t.ExternalLeagueId != null)
                .Where(t => t.Games.Any(g => !g.IsFinished && g.StartTime <= now && g.ExternalFixtureId != null))
                .Select(t => t.Id)
                .ToListAsync(stoppingToken);

            if (tournamentIds.Count == 0)
                return;

            _logger.LogInformation("Score sync: {Count} tournament(s) with active games", tournamentIds.Count);

            foreach (var id in tournamentIds)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                try
                {
                    var updated = await syncService.SyncScoresAsync(id);
                    if (updated > 0)
                        _logger.LogInformation("Score sync: {Updated} game(s) updated in tournament {Id}", updated, id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Score sync failed for tournament {Id}", id);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — swallow
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Score sync job encountered an unexpected error");
        }
    }
}
