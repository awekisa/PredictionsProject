using System.ComponentModel.DataAnnotations;

namespace PredictionsAPI.DTOs.Tournaments;

public class UpdateTournamentRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
