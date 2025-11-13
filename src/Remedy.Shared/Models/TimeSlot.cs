using System.ComponentModel.DataAnnotations;

namespace Remedy.Shared.Models;

public class TimeSlot : SyncableEntity
{

    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Cron expression for recurrence (e.g., "0 9 * * 1-5" for weekdays at 9 AM)
    /// For Phase 1, we can use simple string patterns
    /// </summary>
    public string? RecurrencePattern { get; set; }

    /// <summary>
    /// Typical duration in minutes
    /// </summary>
    public int TypicalDurationMinutes { get; set; }

    public EnergyLevel TypicalEnergy { get; set; }

    /// <summary>
    /// Comma-separated list of compatible resource types
    /// </summary>
    public string ActivityTypes { get; set; } = string.Empty;

    public ICollection<Resource> Resources { get; set; } = new List<Resource>();

    /// <summary>
    /// Gets the list of compatible resource types
    /// </summary>
    public List<ResourceType> GetActivityTypes()
    {
        if (string.IsNullOrWhiteSpace(ActivityTypes))
            return new List<ResourceType>();

        return ActivityTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.Parse<ResourceType>(s.Trim()))
            .ToList();
    }

    /// <summary>
    /// Sets the compatible resource types
    /// </summary>
    public void SetActivityTypes(IEnumerable<ResourceType> types)
    {
        ActivityTypes = string.Join(",", types);
    }
}
