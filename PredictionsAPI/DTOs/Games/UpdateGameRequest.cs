using System.ComponentModel.DataAnnotations;

namespace PredictionsAPI.DTOs.Games;

public class UpdateGameRequest
{
    [Required]
    public string HomeTeam { get; set; } = string.Empty;

    [Required]
    public string AwayTeam { get; set; } = string.Empty;

    [Required]
    public DateTime StartTime { get; set; }
}
