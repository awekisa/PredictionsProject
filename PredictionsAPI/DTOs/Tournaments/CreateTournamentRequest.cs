using System.ComponentModel.DataAnnotations;

namespace PredictionsAPI.DTOs.Tournaments;

public class CreateTournamentRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;
}
