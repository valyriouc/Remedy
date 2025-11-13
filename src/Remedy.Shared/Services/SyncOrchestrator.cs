using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.DTOs;
using Remedy.Shared.Models;

namespace Remedy.Shared.Services;

/// <summary>
/// Orchestrates synchronization between local database and server
/// </summary>
public class SyncOrchestrator
{
    private readonly RemedyDbContext _context;
    private readonly SyncService _syncService;
    private readonly SyncConfiguration _config;
    private HttpSyncClient? _httpClient;

    public SyncOrchestrator(RemedyDbContext context)
    {
        _context = context;
        _syncService = new SyncService(context);
        _config = SyncConfiguration.Load();

        if (_config.IsSyncEnabled())
        {
            _httpClient = new HttpSyncClient(
                _config.ServerUrl!,
                _config.MaxRetryAttempts,
                _config.InitialRetryDelayMs
            );
        }
    }

    /// <summary>
    /// Performs full synchronization: push local changes, then pull server changes
    /// </summary>
    public async Task<SyncResult> SynchronizeAsync()
    {
        if (!_config.IsSyncEnabled() || _httpClient == null)
        {
            return new SyncResult
            {
                Success = true,
                Message = "Sync disabled - running in offline-only mode"
            };
        }

        // Check if server is available
        var isAvailable = await _httpClient.IsServerAvailableAsync();
        if (!isAvailable)
        {
            return new SyncResult
            {
                Success = false,
                Message = "Server unavailable - changes will sync when server is back online"
            };
        }

        var result = new SyncResult();

        try
        {
            // Step 1: Push local changes
            var pushResult = await PushChangesAsync();
            result.PushedCount = pushResult.SuccessCount;
            result.PushFailedCount = pushResult.FailedCount;

            // Step 2: Pull server changes
            var pullResult = await PullChangesAsync();
            result.PulledCount = pullResult.UpdatedCount;

            result.Success = true;
            result.Message = $"Synced: Pushed {result.PushedCount}, Pulled {result.PulledCount}";

            // Update last sync time
            _config.LastSyncTime = DateTime.UtcNow;
            _config.Save();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Sync error: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Pushes local pending changes to server
    /// </summary>
    private async Task<(int SuccessCount, int FailedCount)> PushChangesAsync()
    {
        if (_httpClient == null)
            return (0, 0);

        // Get all pending items
        var pendingResources = await _syncService.GetPendingSyncResourcesAsync();
        var pendingTimeSlots = await _syncService.GetPendingSyncTimeSlotsAsync();

        if (pendingResources.Count == 0 && pendingTimeSlots.Count == 0)
        {
            return (0, 0);
        }

        // Prepare batch sync request
        var request = new BatchSyncRequest
        {
            Resources = pendingResources.Select(r => new ResourceSyncDto
            {
                Id = r.Id,
                ServerId = r.ServerId,
                Type = r.Type.ToString(),
                Url = r.Url,
                Title = r.Title,
                Description = r.Description,
                EstimatedTimeMinutes = r.EstimatedTimeMinutes,
                Difficulty = r.Difficulty.ToString(),
                SavedAt = r.SavedAt,
                CreatedByContext = r.CreatedByContext,
                TargetTimeframe = r.TargetTimeframe.ToString(),
                PreferredTimeSlotId = r.PreferredTimeSlotId,
                MinEnergyLevel = r.MinEnergyLevel.ToString(),
                IsRecurring = r.IsRecurring,
                Priority = r.Priority,
                LastReminded = r.LastReminded,
                TimesSnoozed = r.TimesSnoozed,
                RelevanceScore = r.RelevanceScore,
                IsCompleted = r.IsCompleted,
                CompletedAt = r.CompletedAt,
                Rating = r.Rating,
                ModifiedAt = r.ModifiedAt,
                IsDeleted = r.IsDeleted,
                Version = r.Version
            }).ToList(),

            TimeSlots = pendingTimeSlots.Select(t => new TimeSlotSyncDto
            {
                Id = t.Id,
                ServerId = t.ServerId,
                Name = t.Name,
                RecurrencePattern = t.RecurrencePattern,
                TypicalDurationMinutes = t.TypicalDurationMinutes,
                TypicalEnergy = t.TypicalEnergy.ToString(),
                ActivityTypes = t.ActivityTypes,
                ModifiedAt = t.ModifiedAt,
                IsDeleted = t.IsDeleted,
                Version = t.Version
            }).ToList()
        };

        // Send batch sync
        var (success, response, error) = await _httpClient.BatchSyncAsync(request);

        if (!success || response == null)
        {
            // Mark all as failed
            foreach (var resource in pendingResources)
            {
                _syncService.MarkAsSyncFailed(resource, error ?? "Unknown error");
            }
            foreach (var timeSlot in pendingTimeSlots)
            {
                _syncService.MarkAsSyncFailed(timeSlot, error ?? "Unknown error");
            }

            await _context.SaveChangesAsync();
            return (0, pendingResources.Count + pendingTimeSlots.Count);
        }

        // Process results
        int successCount = 0;
        int failedCount = 0;

        // Update resources based on sync results
        foreach (var result in response.ResourceResults)
        {
            var resource = pendingResources.FirstOrDefault(r => r.Id == result.ClientId);
            if (resource != null)
            {
                if (result.Success)
                {
                    _syncService.MarkAsSynced(resource, result.ServerId);
                    successCount++;
                }
                else
                {
                    _syncService.MarkAsSyncFailed(resource, result.Error ?? "Unknown error");
                    failedCount++;
                }
            }
        }

        // Update time slots based on sync results
        foreach (var result in response.TimeSlotResults)
        {
            var timeSlot = pendingTimeSlots.FirstOrDefault(t => t.Id == result.ClientId);
            if (timeSlot != null)
            {
                if (result.Success)
                {
                    _syncService.MarkAsSynced(timeSlot, result.ServerId);
                    successCount++;
                }
                else
                {
                    _syncService.MarkAsSyncFailed(timeSlot, result.Error ?? "Unknown error");
                    failedCount++;
                }
            }
        }

        await _context.SaveChangesAsync();

        // Purge synced deleted entities
        await _syncService.PurgeDeletedEntitiesAsync();

        return (successCount, failedCount);
    }

    /// <summary>
    /// Pulls server changes and applies them locally
    /// </summary>
    private async Task<(int UpdatedCount, int ConflictCount)> PullChangesAsync()
    {
        if (_httpClient == null)
            return (0, 0);

        var lastSync = _config.LastSyncTime ?? DateTime.MinValue;

        var (success, data, error) = await _httpClient.PullChangesAsync(lastSync);

        if (!success || data == null)
        {
            return (0, 0);
        }

        int updatedCount = 0;
        int conflictCount = 0;

        // Apply server resources
        foreach (var dto in data.Resources)
        {
            var existing = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == dto.Id || (dto.ServerId.HasValue && r.ServerId == dto.ServerId));

            if (existing == null)
            {
                // Create new from server
                var resource = new Resource
                {
                    Id = dto.Id,
                    ServerId = dto.ServerId,
                    Type = Enum.Parse<ResourceType>(dto.Type),
                    Url = dto.Url,
                    Title = dto.Title,
                    Description = dto.Description,
                    EstimatedTimeMinutes = dto.EstimatedTimeMinutes,
                    Difficulty = Enum.Parse<Difficulty>(dto.Difficulty),
                    SavedAt = dto.SavedAt,
                    CreatedByContext = dto.CreatedByContext,
                    TargetTimeframe = Enum.Parse<TargetTimeframe>(dto.TargetTimeframe),
                    PreferredTimeSlotId = dto.PreferredTimeSlotId,
                    MinEnergyLevel = Enum.Parse<EnergyLevel>(dto.MinEnergyLevel),
                    IsRecurring = dto.IsRecurring,
                    Priority = dto.Priority,
                    LastReminded = dto.LastReminded,
                    TimesSnoozed = dto.TimesSnoozed,
                    RelevanceScore = dto.RelevanceScore,
                    IsCompleted = dto.IsCompleted,
                    CompletedAt = dto.CompletedAt,
                    Rating = dto.Rating,
                    ModifiedAt = dto.ModifiedAt,
                    IsDeleted = dto.IsDeleted,
                    Version = dto.Version,
                    SyncStatus = SyncStatus.Synced,
                    LastSyncedAt = DateTime.UtcNow
                };

                _context.Resources.Add(resource);
                updatedCount++;
            }
            else if (existing.ModifiedAt < dto.ModifiedAt && existing.SyncStatus == SyncStatus.Synced)
            {
                // Server has newer version and no local changes - update
                existing.Type = Enum.Parse<ResourceType>(dto.Type);
                existing.Url = dto.Url;
                existing.Title = dto.Title;
                existing.Description = dto.Description;
                existing.EstimatedTimeMinutes = dto.EstimatedTimeMinutes;
                existing.Difficulty = Enum.Parse<Difficulty>(dto.Difficulty);
                existing.CreatedByContext = dto.CreatedByContext;
                existing.TargetTimeframe = Enum.Parse<TargetTimeframe>(dto.TargetTimeframe);
                existing.PreferredTimeSlotId = dto.PreferredTimeSlotId;
                existing.MinEnergyLevel = Enum.Parse<EnergyLevel>(dto.MinEnergyLevel);
                existing.IsRecurring = dto.IsRecurring;
                existing.Priority = dto.Priority;
                existing.LastReminded = dto.LastReminded;
                existing.TimesSnoozed = dto.TimesSnoozed;
                existing.RelevanceScore = dto.RelevanceScore;
                existing.IsCompleted = dto.IsCompleted;
                existing.CompletedAt = dto.CompletedAt;
                existing.Rating = dto.Rating;
                existing.ModifiedAt = dto.ModifiedAt;
                existing.IsDeleted = dto.IsDeleted;
                existing.Version = dto.Version;
                existing.LastSyncedAt = DateTime.UtcNow;

                updatedCount++;
            }
            else if (existing.ModifiedAt < dto.ModifiedAt && existing.SyncStatus != SyncStatus.Synced)
            {
                // Conflict: both client and server have changes
                conflictCount++;
                // Keep local version but log conflict
                // In a production system, you might want to handle this differently
            }
        }

        // Apply server time slots (similar logic)
        foreach (var dto in data.TimeSlots)
        {
            var existing = await _context.TimeSlots
                .FirstOrDefaultAsync(t => t.Id == dto.Id || (dto.ServerId.HasValue && t.ServerId == dto.ServerId));

            if (existing == null)
            {
                var timeSlot = new TimeSlot
                {
                    Id = dto.Id,
                    ServerId = dto.ServerId,
                    Name = dto.Name,
                    RecurrencePattern = dto.RecurrencePattern,
                    TypicalDurationMinutes = dto.TypicalDurationMinutes,
                    TypicalEnergy = Enum.Parse<EnergyLevel>(dto.TypicalEnergy),
                    ActivityTypes = dto.ActivityTypes,
                    ModifiedAt = dto.ModifiedAt,
                    IsDeleted = dto.IsDeleted,
                    Version = dto.Version,
                    SyncStatus = SyncStatus.Synced,
                    LastSyncedAt = DateTime.UtcNow
                };

                _context.TimeSlots.Add(timeSlot);
                updatedCount++;
            }
            else if (existing.ModifiedAt < dto.ModifiedAt && existing.SyncStatus == SyncStatus.Synced)
            {
                existing.Name = dto.Name;
                existing.RecurrencePattern = dto.RecurrencePattern;
                existing.TypicalDurationMinutes = dto.TypicalDurationMinutes;
                existing.TypicalEnergy = Enum.Parse<EnergyLevel>(dto.TypicalEnergy);
                existing.ActivityTypes = dto.ActivityTypes;
                existing.ModifiedAt = dto.ModifiedAt;
                existing.IsDeleted = dto.IsDeleted;
                existing.Version = dto.Version;
                existing.LastSyncedAt = DateTime.UtcNow;

                updatedCount++;
            }
            else if (existing.ModifiedAt < dto.ModifiedAt && existing.SyncStatus != SyncStatus.Synced)
            {
                conflictCount++;
            }
        }

        await _context.SaveChangesAsync();

        return (updatedCount, conflictCount);
    }

    /// <summary>
    /// Gets current sync status
    /// </summary>
    public async Task<string> GetSyncStatusAsync()
    {
        if (!_config.IsSyncEnabled())
        {
            return "Sync: DISABLED (offline-only mode)";
        }

        var pendingCount = await _syncService.GetPendingSyncCountAsync();

        if (pendingCount == 0)
        {
            var lastSync = _config.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "never";
            return $"Sync: UP TO DATE (last sync: {lastSync})";
        }

        return $"Sync: {pendingCount} items pending";
    }
}

/// <summary>
/// Result of a sync operation
/// </summary>
public class SyncResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int PushedCount { get; set; }
    public int PulledCount { get; set; }
    public int PushFailedCount { get; set; }
}
