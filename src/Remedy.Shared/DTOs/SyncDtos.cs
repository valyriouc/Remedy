namespace Remedy.Shared.DTOs;

/// <summary>
/// Response from sync operations
/// </summary>
public class SyncResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public Guid? ServerId { get; set; }
    public int? ServerVersion { get; set; }
    public DateTime? ServerModifiedAt { get; set; }
}

/// <summary>
/// Batch sync request
/// </summary>
public class BatchSyncRequest
{
    public List<ResourceSyncDto> Resources { get; set; } = new();
    public List<TimeSlotSyncDto> TimeSlots { get; set; } = new();
}

/// <summary>
/// Batch sync response
/// </summary>
public class BatchSyncResponse
{
    public List<SyncItemResult> ResourceResults { get; set; } = new();
    public List<SyncItemResult> TimeSlotResults { get; set; } = new();
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

/// <summary>
/// Result for individual sync item
/// </summary>
public class SyncItemResult
{
    public Guid ClientId { get; set; }
    public Guid? ServerId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool IsConflict { get; set; }
}

/// <summary>
/// Resource data for sync operations
/// </summary>
public class ResourceSyncDto
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int EstimatedTimeMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
    public string CreatedByContext { get; set; } = string.Empty;
    public string TargetTimeframe { get; set; } = string.Empty;
    public Guid? PreferredTimeSlotId { get; set; }
    public string MinEnergyLevel { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public double Priority { get; set; }
    public DateTime? LastReminded { get; set; }
    public int TimesSnoozed { get; set; }
    public double RelevanceScore { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? Rating { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Time slot data for sync operations
/// </summary>
public class TimeSlotSyncDto
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RecurrencePattern { get; set; }
    public int TypicalDurationMinutes { get; set; }
    public string TypicalEnergy { get; set; } = string.Empty;
    public string ActivityTypes { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public bool IsDeleted { get; set; }
    public int Version { get; set; }
}
