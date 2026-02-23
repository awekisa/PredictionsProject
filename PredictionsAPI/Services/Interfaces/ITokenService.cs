using PredictionsAPI.Entities;

namespace PredictionsAPI.Services.Interfaces;

public interface ITokenService
{
    Task<string> GenerateTokenAsync(ApplicationUser user);
}
