using Microsoft.EntityFrameworkCore;
using PredictionsAPI.Data;

namespace PredictionsAPI.Services.Implementations;

public class AdminDeletionService
{
    private readonly AppDbContext _context;

    public AdminDeletionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> DeletePredictionAsync(int predictionId)
    {
        var prediction = await _context.Predictions.FindAsync(predictionId);
        if (prediction is null) return false;

        _context.Predictions.Remove(prediction);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null) return false;

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<AdminUserResponse>> GetUsersAsync()
    {
        return await _context.Users
            .OrderBy(u => u.DisplayName)
            .Select(u => new AdminUserResponse
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                DisplayName = u.DisplayName,
                Roles = _context.UserRoles
                    .Where(ur => ur.UserId == u.Id)
                    .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name ?? string.Empty)
                    .ToList(),
                PredictionCount = u.Predictions.Count
            })
            .ToListAsync();
    }

    public async Task<List<AdminPredictionResponse>> GetPredictionsAsync()
    {
        return await _context.Predictions
            .Include(p => p.Game)
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AdminPredictionResponse
            {
                Id = p.Id,
                GameId = p.GameId,
                HomeTeam = p.Game.HomeTeam,
                AwayTeam = p.Game.AwayTeam,
                HomeGoals = p.HomeGoals,
                AwayGoals = p.AwayGoals,
                UserDisplayName = p.User.DisplayName,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();
    }
}

public class AdminUserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public int PredictionCount { get; set; }
}

public class AdminPredictionResponse
{
    public int Id { get; set; }
    public int GameId { get; set; }
    public string HomeTeam { get; set; } = string.Empty;
    public string AwayTeam { get; set; } = string.Empty;
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
