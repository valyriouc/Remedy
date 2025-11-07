using Microsoft.EntityFrameworkCore;
using Remedy.Cli.Data;
using Remedy.Cli.Models;

namespace Remedy.Cli.Commands;

public static class SaveCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var url = parser.GetArgument(1);
        var title = parser.GetOption("--title", "-t");
        var type = parser.GetOption<ResourceType>(ResourceType.Article, "--type");
        var time = parser.GetIntOption(15, "--time");
        var difficulty = parser.GetOption<Difficulty>(Difficulty.Medium, "--difficulty", "-d");
        var slotName = parser.GetOption("--slot", "-s");
        var context = parser.GetOption("--context", "-c");
        var timeframe = parser.GetOption<TargetTimeframe>(TargetTimeframe.ThisWeek, "--timeframe", "-tf");
        var energy = parser.GetOption<EnergyLevel>(EnergyLevel.Medium, "--energy", "-e");
        var description = parser.GetOption("--description");

        await ExecuteInternalAsync(url, title, type, time, difficulty, slotName, context, timeframe, energy, description);
    }

    private static async Task ExecuteInternalAsync(
        string? url,
        string? title,
        ResourceType type,
        int time,
        Difficulty difficulty,
        string? slotName,
        string? context,
        TargetTimeframe timeframe,
        EnergyLevel energy,
        string? description)
    {
        using var db = new RemedyDbContext();

        // Ensure database is created
        await db.Database.EnsureCreatedAsync();

        // If no title provided, prompt or use URL
        if (string.IsNullOrWhiteSpace(title))
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Console.Write("Enter title: ");
                title = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(title))
                {
                    Console.WriteLine("Error: Title is required.");
                    return;
                }
            }
            else
            {
                title = url;
            }
        }

        // Find time slot if specified
        Guid? timeSlotId = null;
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            var timeSlot = await db.TimeSlots
                .FirstOrDefaultAsync(ts => ts.Name.ToLower() == slotName.ToLower());

            if (timeSlot == null)
            {
                Console.WriteLine($"Warning: Time slot '{slotName}' not found. Resource will be saved without a time slot.");
                Console.WriteLine("Use 'remedy config slots' to create time slots.");
            }
            else
            {
                timeSlotId = timeSlot.Id;
            }
        }

        var resource = new Resource
        {
            Id = Guid.NewGuid(),
            Url = url,
            Title = title,
            Description = description ?? string.Empty,
            Type = type,
            EstimatedTimeMinutes = time,
            Difficulty = difficulty,
            SavedAt = DateTime.Now,
            CreatedByContext = context ?? string.Empty,
            TargetTimeframe = timeframe,
            PreferredTimeSlotId = timeSlotId,
            MinEnergyLevel = energy,
            Priority = 1.0,
            RelevanceScore = 1.0
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync();

        Console.WriteLine($"✓ Resource saved: {title}");
        Console.WriteLine($"  Type: {type}, Time: {time} min, Difficulty: {difficulty}");
        if (timeSlotId.HasValue)
        {
            Console.WriteLine($"  Time Slot: {slotName}");
        }
        Console.WriteLine($"  Target: {timeframe}");
        Console.WriteLine($"  ID: {resource.Id}");
    }
}
