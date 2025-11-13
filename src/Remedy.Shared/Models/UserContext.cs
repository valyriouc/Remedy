
namespace Remedy.Shared.Models;

/// <summary>
/// Represents the current user context for resource matching
/// </summary>
public class UserContext
{
    public DateTime CurrentTime { get; set; } = DateTime.Now;

    public EnergyLevel CurrentEnergy { get; set; } = EnergyLevel.Medium;

    /// <summary>
    /// Available duration in minutes
    /// </summary>
    public int AvailableDurationMinutes { get; set; } = 30;

    public string CurrentContextDescription { get; set; } = string.Empty;

    public Guid? ActiveTimeSlotId { get; set; }
}
