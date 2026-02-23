using Microsoft.AspNetCore.Identity;

namespace PredictionsAPI.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
}
