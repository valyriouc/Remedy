using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.Models;

namespace Remedy.Shared.Services;

/// <summary>
/// Service for managing offline-first synchronization with the server
/// </summary>
public class SyncService
{
    private readonly RemedyDbContext _context;
    private readonly string? _serverUrl;

    public SyncService(RemedyDbContext context, string? serverUrl = null)
    {
        _context = context;
        _serverUrl = serverUrl;
    }

    /// <summary>
    /// Marks an entity as modified and needing sync
    /// </summary>
    public void MarkForSync<T>(T entity) where T : SyncableEntity
    {
        entity.ModifiedAt = DateTime.UtcNow;
        entity.Version++;

        // Update sync status based on current state
        entity.SyncStatus = entity.SyncStatus == SyncStatus.Synced
            ? SyncStatus.PendingSync
            : entity.SyncStatus;

        if (entity.SyncStatus == SyncStatus.LocalOnly)
        {
            entity.SyncStatus = SyncStatus.PendingSync;
        }
    }

    /// <summary>
    /// Marks an entity for deletion (soft delete)
    /// </summary>
    public void MarkForDeletion<T>(T entity) where T : SyncableEntity
    {
        entity.IsDeleted = true;
        entity.ModifiedAt = DateTime.UtcNow;
        entity.Version++;
        entity.SyncStatus = SyncStatus.PendingSync;
    }

    /// <summary>
    /// Gets all entities pending synchronization
    /// </summary>
    public async Task<List<Resource>> GetPendingSyncResourcesAsync()
    {
        return await _context.Resources
            .Where(r => r.SyncStatus == SyncStatus.PendingSync || r.SyncStatus == SyncStatus.SyncFailed)
            .OrderBy(r => r.ModifiedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Gets all time slots pending synchronization
    /// </summary>
    public async Task<List<TimeSlot>> GetPendingSyncTimeSlotsAsync()
    {
        return await _context.TimeSlots
            .Where(t => t.SyncStatus == SyncStatus.PendingSync || t.SyncStatus == SyncStatus.SyncFailed)
            .OrderBy(t => t.ModifiedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Marks an entity as successfully synced
    /// </summary>
    public void MarkAsSynced<T>(T entity, Guid? serverId = null) where T : SyncableEntity
    {
        entity.SyncStatus = SyncStatus.Synced;
        entity.LastSyncedAt = DateTime.UtcNow;
        entity.SyncRetryCount = 0;
        entity.LastSyncError = null;

        if (serverId.HasValue)
        {
            entity.ServerId = serverId;
        }
    }

    /// <summary>
    /// Marks an entity as having failed sync
    /// </summary>
    public void MarkAsSyncFailed<T>(T entity, string error) where T : SyncableEntity
    {
        entity.SyncStatus = SyncStatus.SyncFailed;
        entity.SyncRetryCount++;
        entity.LastSyncError = error;
    }

    /// <summary>
    /// Checks if server is available
    /// </summary>
    public bool IsServerConfigured()
    {
        return !string.IsNullOrWhiteSpace(_serverUrl);
    }

    /// <summary>
    /// Gets count of entities pending sync
    /// </summary>
    public async Task<int> GetPendingSyncCountAsync()
    {
        var resourceCount = await _context.Resources
            .CountAsync(r => r.SyncStatus == SyncStatus.PendingSync || r.SyncStatus == SyncStatus.SyncFailed);

        var timeSlotCount = await _context.TimeSlots
            .CountAsync(t => t.SyncStatus == SyncStatus.PendingSync || t.SyncStatus == SyncStatus.SyncFailed);

        return resourceCount + timeSlotCount;
    }

    /// <summary>
    /// Resolves a conflict by choosing local or server version
    /// </summary>
    public void ResolveConflict<T>(T entity, bool useLocalVersion) where T : SyncableEntity
    {
        if (useLocalVersion)
        {
            entity.SyncStatus = SyncStatus.PendingSync;
            entity.ModifiedAt = DateTime.UtcNow;
        }
        else
        {
            // Server version wins - mark as synced
            entity.SyncStatus = SyncStatus.Synced;
            entity.LastSyncedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Resets sync retry count for failed items (useful after fixing server issues)
    /// </summary>
    public async Task ResetFailedSyncsAsync()
    {
        var failedResources = await _context.Resources
            .Where(r => r.SyncStatus == SyncStatus.SyncFailed)
            .ToListAsync();

        var failedTimeSlots = await _context.TimeSlots
            .Where(t => t.SyncStatus == SyncStatus.SyncFailed)
            .ToListAsync();

        foreach (var resource in failedResources)
        {
            resource.SyncStatus = SyncStatus.PendingSync;
            resource.SyncRetryCount = 0;
            resource.LastSyncError = null;
        }

        foreach (var timeSlot in failedTimeSlots)
        {
            timeSlot.SyncStatus = SyncStatus.PendingSync;
            timeSlot.SyncRetryCount = 0;
            timeSlot.LastSyncError = null;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Gets entities that have been deleted locally and need sync
    /// </summary>
    public async Task<List<Resource>> GetDeletedResourcesAsync()
    {
        return await _context.Resources
            .Where(r => r.IsDeleted && r.SyncStatus == SyncStatus.PendingSync)
            .ToListAsync();
    }

    /// <summary>
    /// Permanently removes entities that have been synced as deleted
    /// </summary>
    public async Task PurgeDeletedEntitiesAsync()
    {
        var deletedResources = await _context.Resources
            .Where(r => r.IsDeleted && r.SyncStatus == SyncStatus.Synced)
            .ToListAsync();

        var deletedTimeSlots = await _context.TimeSlots
            .Where(t => t.IsDeleted && t.SyncStatus == SyncStatus.Synced)
            .ToListAsync();

        _context.Resources.RemoveRange(deletedResources);
        _context.TimeSlots.RemoveRange(deletedTimeSlots);

        await _context.SaveChangesAsync();
    }
}
