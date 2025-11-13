using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.DTOs;
using Remedy.Shared.Models;

namespace Remedy.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResourcesController : ControllerBase
{
    private readonly RemedyDbContext _context;

    public ResourcesController(RemedyDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all resources
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ResourceSyncDto>>> GetAll([FromQuery] DateTime? modifiedSince = null)
    {
        var query = _context.Resources.Where(r => !r.IsDeleted);

        if (modifiedSince.HasValue)
        {
            query = query.Where(r => r.ModifiedAt > modifiedSince.Value);
        }

        var resources = await query.ToListAsync();

        var dtos = resources.Select(r => new ResourceSyncDto
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
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get a specific resource by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ResourceSyncDto>> GetById(Guid id)
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null || resource.IsDeleted)
        {
            return NotFound();
        }

        var dto = new ResourceSyncDto
        {
            Id = resource.Id,
            ServerId = resource.ServerId,
            Type = resource.Type.ToString(),
            Url = resource.Url,
            Title = resource.Title,
            Description = resource.Description,
            EstimatedTimeMinutes = resource.EstimatedTimeMinutes,
            Difficulty = resource.Difficulty.ToString(),
            SavedAt = resource.SavedAt,
            CreatedByContext = resource.CreatedByContext,
            TargetTimeframe = resource.TargetTimeframe.ToString(),
            PreferredTimeSlotId = resource.PreferredTimeSlotId,
            MinEnergyLevel = resource.MinEnergyLevel.ToString(),
            IsRecurring = resource.IsRecurring,
            Priority = resource.Priority,
            LastReminded = resource.LastReminded,
            TimesSnoozed = resource.TimesSnoozed,
            RelevanceScore = resource.RelevanceScore,
            IsCompleted = resource.IsCompleted,
            CompletedAt = resource.CompletedAt,
            Rating = resource.Rating,
            ModifiedAt = resource.ModifiedAt,
            IsDeleted = resource.IsDeleted,
            Version = resource.Version
        };

        return Ok(dto);
    }

    /// <summary>
    /// Create or update a resource
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResponse>> CreateOrUpdate([FromBody] ResourceSyncDto dto)
    {
        var existing = await _context.Resources
            .FirstOrDefaultAsync(r => r.Id == dto.Id || (dto.ServerId.HasValue && r.ServerId == dto.ServerId));

        if (existing != null)
        {
            // Conflict detection
            if (existing.Version != dto.Version && existing.ModifiedAt > dto.ModifiedAt)
            {
                return Conflict(new SyncResponse
                {
                    Success = false,
                    Message = "Version conflict - server has newer version",
                    ServerId = existing.ServerId ?? existing.Id,
                    ServerVersion = existing.Version,
                    ServerModifiedAt = existing.ModifiedAt
                });
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
            existing.IsDeleted = dto.IsDeleted;

            await _context.SaveChangesAsync();

            return Ok(new SyncResponse
            {
                Success = true,
                Message = "Resource updated",
                ServerId = existing.ServerId ?? existing.Id,
                ServerVersion = existing.Version,
                ServerModifiedAt = existing.ModifiedAt
            });
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
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = resource.Id }, new SyncResponse
        {
            Success = true,
            Message = "Resource created",
            ServerId = resource.Id,
            ServerVersion = resource.Version,
            ServerModifiedAt = resource.ModifiedAt
        });
    }

    /// <summary>
    /// Delete a resource
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<SyncResponse>> Delete(Guid id)
    {
        var resource = await _context.Resources.FindAsync(id);

        if (resource == null)
        {
            return NotFound();
        }

        resource.IsDeleted = true;
        resource.ModifiedAt = DateTime.UtcNow;
        resource.Version++;

        await _context.SaveChangesAsync();

        return Ok(new SyncResponse
        {
            Success = true,
            Message = "Resource deleted",
            ServerId = resource.ServerId ?? resource.Id
        });
    }
}
