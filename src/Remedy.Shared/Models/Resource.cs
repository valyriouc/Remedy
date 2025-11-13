using System.ComponentModel.DataAnnotations;

namespace Remedy.Shared.Models;

public class Resource : SyncableEntity
{

    public ResourceType Type { get; set; }

    public string? Url { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Estimated time to consume in minutes
    /// </summary>
    public int EstimatedTimeMinutes { get; set; }

    public Difficulty Difficulty { get; set; }

    // Temporal metadata
    public DateTime SavedAt { get; set; }

    public string CreatedByContext { get; set; } = string.Empty;

    public TargetTimeframe TargetTimeframe { get; set; }

    // Scheduling
    public Guid? PreferredTimeSlotId { get; set; }
    public TimeSlot? PreferredTimeSlot { get; set; }

    public EnergyLevel MinEnergyLevel { get; set; }

    public bool IsRecurring { get; set; }

    // Decay & Priority
    /// <summary>
    /// Priority score from 0.0 to 1.0
    /// </summary>
    public double Priority { get; set; } = 1.0;

    public DateTime? LastReminded { get; set; }

    public int TimesSnoozed { get; set; }

    public double RelevanceScore { get; set; } = 1.0;

    // Completion tracking
    public bool IsCompleted { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int? Rating { get; set; }

    /// <summary>
    /// Transient property for computed matching score
    /// </summary>
    public double ComputedScore { get; set; }
}
