using System.ComponentModel.DataAnnotations;

namespace PredictionsAPI.DTOs.Games;

public class SetGameResultRequest
{
    [Required]
    [Range(0, int.MaxValue)]
    public int HomeGoals { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int AwayGoals { get; set; }
}
