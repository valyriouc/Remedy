using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.DTOs;
using Remedy.Shared.Models;

namespace Remedy.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly RemedyDbContext _context;

    public SyncController(RemedyDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Batch sync resources and time slots
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<BatchSyncResponse>> BatchSync([FromBody] BatchSyncRequest request)
    {
        var response = new BatchSyncResponse();

        // Process resources
        foreach (var dto in request.Resources)
        {
            var result = await SyncResource(dto);
            response.ResourceResults.Add(result);

            if (result.Success)
                response.SuccessCount++;
            else
                response.FailureCount++;
        }

        // Process time slots
        foreach (var dto in request.TimeSlots)
        {
            var result = await SyncTimeSlot(dto);
            response.TimeSlotResults.Add(result);

            if (result.Success)
                response.SuccessCount++;
            else
                response.FailureCount++;
        }

        await _context.SaveChangesAsync();

        return Ok(response);
    }

    /// <summary>
    /// Pull changes from server since last sync
    /// </summary>
    [HttpGet("pull")]
    public async Task<ActionResult<BatchSyncRequest>> Pull([FromQuery] DateTime? since = null)
    {
        var modifiedSince = since ?? DateTime.MinValue;

        var resources = await _context.Resources
            .Where(r => r.ModifiedAt > modifiedSince)
            .ToListAsync();

        var timeSlots = await _context.TimeSlots
            .Where(t => t.ModifiedAt > modifiedSince)
            .ToListAsync();

        var response = new BatchSyncRequest
        {
            Resources = resources.Select(r => new ResourceSyncDto
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

            TimeSlots = timeSlots.Select(t => new TimeSlotSyncDto
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

        return Ok(response);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    private async Task<SyncItemResult> SyncResource(ResourceSyncDto dto)
    {
        try
        {
            var existing = await _context.Resources
                .FirstOrDefaultAsync(r => r.Id == dto.Id || (dto.ServerId.HasValue && r.ServerId == dto.ServerId));

            if (existing != null)
            {
                // Conflict detection
                if (existing.Version != dto.Version && existing.ModifiedAt > dto.ModifiedAt)
                {
                    return new SyncItemResult
                    {
                        ClientId = dto.Id,
                        ServerId = existing.ServerId ?? existing.Id,
                        Success = false,
                        Error = "Version conflict",
                        IsConflict = true
                    };
                }

                // Handle deletions
                if (dto.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.ModifiedAt = DateTime.UtcNow;
                    existing.Version++;

                    return new SyncItemResult
                    {
                        ClientId = dto.Id,
                        ServerId = existing.ServerId ?? existing.Id,
                        Success = true
                    };
                }

                // Update existing
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
                existing.ModifiedAt = DateTime.UtcNow;
                existing.Version++;

                return new SyncItemResult
                {
                    ClientId = dto.Id,
                    ServerId = existing.ServerId ?? existing.Id,
                    Success = true
                };
            }

            // Create new
            var resource = new Resource
            {
                Id = dto.Id,
                ServerId = dto.Id,
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
                ModifiedAt = DateTime.UtcNow,
                IsDeleted = dto.IsDeleted,
                Version = 1,
                SyncStatus = SyncStatus.Synced,
                LastSyncedAt = DateTime.UtcNow
            };

            _context.Resources.Add(resource);

            return new SyncItemResult
            {
                ClientId = dto.Id,
                ServerId = resource.Id,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new SyncItemResult
            {
                ClientId = dto.Id,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private async Task<SyncItemResult> SyncTimeSlot(TimeSlotSyncDto dto)
    {
        try
        {
            var existing = await _context.TimeSlots
                .FirstOrDefaultAsync(t => t.Id == dto.Id || (dto.ServerId.HasValue && t.ServerId == dto.ServerId));

            if (existing != null)
            {
                // Conflict detection
                if (existing.Version != dto.Version && existing.ModifiedAt > dto.ModifiedAt)
                {
                    return new SyncItemResult
                    {
                        ClientId = dto.Id,
                        ServerId = existing.ServerId ?? existing.Id,
                        Success = false,
                        Error = "Version conflict",
                        IsConflict = true
                    };
                }

                // Handle deletions
                if (dto.IsDeleted)
                {
                    existing.IsDeleted = true;
                    existing.ModifiedAt = DateTime.UtcNow;
                    existing.Version++;

                    return new SyncItemResult
                    {
                        ClientId = dto.Id,
                        ServerId = existing.ServerId ?? existing.Id,
                        Success = true
                    };
                }

                // Update existing
                existing.Name = dto.Name;
                existing.RecurrencePattern = dto.RecurrencePattern;
                existing.TypicalDurationMinutes = dto.TypicalDurationMinutes;
                existing.TypicalEnergy = Enum.Parse<EnergyLevel>(dto.TypicalEnergy);
                existing.ActivityTypes = dto.ActivityTypes;
                existing.ModifiedAt = DateTime.UtcNow;
                existing.Version++;

                return new SyncItemResult
                {
                    ClientId = dto.Id,
                    ServerId = existing.ServerId ?? existing.Id,
                    Success = true
                };
            }

            // Create new
            var timeSlot = new TimeSlot
            {
                Id = dto.Id,
                ServerId = dto.Id,
                Name = dto.Name,
                RecurrencePattern = dto.RecurrencePattern,
                TypicalDurationMinutes = dto.TypicalDurationMinutes,
                TypicalEnergy = Enum.Parse<EnergyLevel>(dto.TypicalEnergy),
                ActivityTypes = dto.ActivityTypes,
                ModifiedAt = DateTime.UtcNow,
                IsDeleted = dto.IsDeleted,
                Version = 1,
                SyncStatus = SyncStatus.Synced,
                LastSyncedAt = DateTime.UtcNow
            };

            _context.TimeSlots.Add(timeSlot);

            return new SyncItemResult
            {
                ClientId = dto.Id,
                ServerId = timeSlot.Id,
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new SyncItemResult
            {
                ClientId = dto.Id,
                Success = false,
                Error = ex.Message
            };
        }
    }
}
