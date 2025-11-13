using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.DTOs;
using Remedy.Shared.Models;

namespace Remedy.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly RemedyDbContext _context;

    public SlotsController(RemedyDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all time slots
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TimeSlotSyncDto>>> GetAll([FromQuery] DateTime? modifiedSince = null)
    {
        var query = _context.TimeSlots.Where(t => !t.IsDeleted);

        if (modifiedSince.HasValue)
        {
            query = query.Where(t => t.ModifiedAt > modifiedSince.Value);
        }

        var timeSlots = await query.ToListAsync();

        var dtos = timeSlots.Select(t => new TimeSlotSyncDto
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
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Get a specific time slot by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TimeSlotSyncDto>> GetById(Guid id)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(id);

        if (timeSlot == null || timeSlot.IsDeleted)
        {
            return NotFound();
        }

        var dto = new TimeSlotSyncDto
        {
            Id = timeSlot.Id,
            ServerId = timeSlot.ServerId,
            Name = timeSlot.Name,
            RecurrencePattern = timeSlot.RecurrencePattern,
            TypicalDurationMinutes = timeSlot.TypicalDurationMinutes,
            TypicalEnergy = timeSlot.TypicalEnergy.ToString(),
            ActivityTypes = timeSlot.ActivityTypes,
            ModifiedAt = timeSlot.ModifiedAt,
            IsDeleted = timeSlot.IsDeleted,
            Version = timeSlot.Version
        };

        return Ok(dto);
    }

    /// <summary>
    /// Create or update a time slot
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SyncResponse>> CreateOrUpdate([FromBody] TimeSlotSyncDto dto)
    {
        var existing = await _context.TimeSlots
            .FirstOrDefaultAsync(t => t.Id == dto.Id || (dto.ServerId.HasValue && t.ServerId == dto.ServerId));

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
            existing.Name = dto.Name;
            existing.RecurrencePattern = dto.RecurrencePattern;
            existing.TypicalDurationMinutes = dto.TypicalDurationMinutes;
            existing.TypicalEnergy = Enum.Parse<EnergyLevel>(dto.TypicalEnergy);
            existing.ActivityTypes = dto.ActivityTypes;
            existing.ModifiedAt = DateTime.UtcNow;
            existing.Version++;
            existing.IsDeleted = dto.IsDeleted;

            await _context.SaveChangesAsync();

            return Ok(new SyncResponse
            {
                Success = true,
                Message = "Time slot updated",
                ServerId = existing.ServerId ?? existing.Id,
                ServerVersion = existing.Version,
                ServerModifiedAt = existing.ModifiedAt
            });
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
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = timeSlot.Id }, new SyncResponse
        {
            Success = true,
            Message = "Time slot created",
            ServerId = timeSlot.Id,
            ServerVersion = timeSlot.Version,
            ServerModifiedAt = timeSlot.ModifiedAt
        });
    }

    /// <summary>
    /// Delete a time slot
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<SyncResponse>> Delete(Guid id)
    {
        var timeSlot = await _context.TimeSlots.FindAsync(id);

        if (timeSlot == null)
        {
            return NotFound();
        }

        timeSlot.IsDeleted = true;
        timeSlot.ModifiedAt = DateTime.UtcNow;
        timeSlot.Version++;

        await _context.SaveChangesAsync();

        return Ok(new SyncResponse
        {
            Success = true,
            Message = "Time slot deleted",
            ServerId = timeSlot.ServerId ?? timeSlot.Id
        });
    }
}