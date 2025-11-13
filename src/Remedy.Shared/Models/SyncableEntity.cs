using System.ComponentModel.DataAnnotations;

namespace Remedy.Shared.Models;

/// <summary>
/// Base class for entities that support offline-first synchronization
/// </summary>
public abstract class SyncableEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Server-assigned ID (null if never synced to server)
    /// </summary>
    public Guid? ServerId { get; set; }

    /// <summary>
    /// Current synchronization status
    /// </summary>
    public SyncStatus SyncStatus { get; set; } = SyncStatus.LocalOnly;

    /// <summary>
    /// Timestamp of last successful sync with server
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Timestamp of last local modification
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Soft delete flag for sync purposes
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Version number for conflict resolution (incremented on each modification)
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Number of sync retry attempts
    /// </summary>
    public int SyncRetryCount { get; set; }

    /// <summary>
    /// Last sync error message (if any)
    /// </summary>
    public string? LastSyncError { get; set; }
}
