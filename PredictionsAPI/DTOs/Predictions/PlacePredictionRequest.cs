using System.ComponentModel.DataAnnotations;

namespace PredictionsAPI.DTOs.Predictions;

public class PlacePredictionRequest
{
    [Required]
    [Range(0, int.MaxValue)]
    public int HomeGoals { get; set; }

    [Required]
    [Range(0, int.MaxValue)]
    public int AwayGoals { get; set; }
}
