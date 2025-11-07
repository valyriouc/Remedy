using Microsoft.EntityFrameworkCore;
using Remedy.Cli.Data;
using Remedy.Cli.Models;

namespace Remedy.Cli.Commands;

public static class ConfigCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var subCommand = parser.GetSubCommand();

        switch (subCommand)
        {
            case "slots":
                await HandleSlotsCommand(parser);
                break;
            case "init":
                await InitializeDefaultSlotsAsync();
                break;
            default:
                ShowConfigHelp();
                break;
        }
    }

    private static async Task HandleSlotsCommand(CommandParser parser)
    {
        var action = parser.GetArgument(2);

        switch (action)
        {
            case "list":
                await ListSlotsAsync();
                break;
            case "add":
                await AddSlotAsync(parser);
                break;
            default:
                Console.WriteLine("Usage:");
                Console.WriteLine("  remedy config slots list");
                Console.WriteLine("  remedy config slots add --name <name> [options]");
                break;
        }
    }

    private static async Task ListSlotsAsync()
    {
        using var db = new RemedyDbContext();
        await db.Database.EnsureCreatedAsync();

        var slots = await db.TimeSlots.ToListAsync();

        if (!slots.Any())
        {
            Console.WriteLine("No time slots configured.");
            Console.WriteLine("\nUse 'remedy config init' to create default slots");
            Console.WriteLine("Or 'remedy config slots add' to create a custom slot");
            return;
        }

        Console.WriteLine($"\nConfigured Time Slots ({slots.Count}):\n");

        foreach (var slot in slots)
        {
            Console.WriteLine($"• {slot.Name}");
            Console.WriteLine($"  Duration: {slot.TypicalDurationMinutes} min | Energy: {slot.TypicalEnergy}");

            var types = slot.GetActivityTypes();
            if (types.Any())
            {
                Console.WriteLine($"  Types: {string.Join(", ", types)}");
            }

            Console.WriteLine($"  ID: {slot.Id}");
            Console.WriteLine();
        }
    }

    private static async Task AddSlotAsync(CommandParser parser)
    {
        var name = parser.GetOption("--name", "-n");
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.WriteLine("Error: --name is required");
            Console.WriteLine("Usage: remedy config slots add --name <name> [--duration <minutes>] [--energy <level>]");
            return;
        }

        var duration = parser.GetIntOption(60, "--duration", "-d");
        var energy = parser.GetOption<EnergyLevel>(EnergyLevel.Medium, "--energy", "-e");
        var typesStr = parser.GetOption("--types", "-t");

        using var db = new RemedyDbContext();
        await db.Database.EnsureCreatedAsync();

        var slot = new TimeSlot
        {
            Id = Guid.NewGuid(),
            Name = name,
            TypicalDurationMinutes = duration,
            TypicalEnergy = energy
        };

        if (!string.IsNullOrWhiteSpace(typesStr))
        {
            var resourceTypes = new List<ResourceType>();
            foreach (var typeStr in typesStr.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<ResourceType>(typeStr.Trim(), true, out var resourceType))
                {
                    resourceTypes.Add(resourceType);
                }
                else
                {
                    Console.WriteLine($"Warning: Unknown resource type '{typeStr}' ignored.");
                }
            }
            slot.SetActivityTypes(resourceTypes);
        }

        db.TimeSlots.Add(slot);
        await db.SaveChangesAsync();

        Console.WriteLine($"✓ Time slot created: {name}");
        Console.WriteLine($"  Duration: {duration} min");
        Console.WriteLine($"  Energy: {energy}");
        if (!string.IsNullOrWhiteSpace(slot.ActivityTypes))
        {
            Console.WriteLine($"  Types: {slot.ActivityTypes}");
        }
        Console.WriteLine($"  ID: {slot.Id}");
    }

    private static async Task InitializeDefaultSlotsAsync()
    {
        using var db = new RemedyDbContext();
        await db.Database.EnsureCreatedAsync();

        var existingCount = await db.TimeSlots.CountAsync();
        if (existingCount > 0)
        {
            Console.Write($"You already have {existingCount} time slot(s). Continue? (y/N): ");
            var response = Console.ReadLine();
            if (response?.ToLowerInvariant() != "y")
            {
                Console.WriteLine("Cancelled.");
                return;
            }
        }

        var defaultSlots = new[]
        {
            new TimeSlot
            {
                Id = Guid.NewGuid(),
                Name = "Morning Deep Work",
                TypicalDurationMinutes = 90,
                TypicalEnergy = EnergyLevel.High,
                ActivityTypes = "Article,Experiment,Book"
            },
            new TimeSlot
            {
                Id = Guid.NewGuid(),
                Name = "Research Hour",
                TypicalDurationMinutes = 60,
                TypicalEnergy = EnergyLevel.Medium,
                ActivityTypes = "Article,Video,Experiment"
            },
            new TimeSlot
            {
                Id = Guid.NewGuid(),
                Name = "Quick Learning",
                TypicalDurationMinutes = 30,
                TypicalEnergy = EnergyLevel.Medium,
                ActivityTypes = "Video,Article"
            },
            new TimeSlot
            {
                Id = Guid.NewGuid(),
                Name = "Weekend Projects",
                TypicalDurationMinutes = 180,
                TypicalEnergy = EnergyLevel.High,
                ActivityTypes = "Experiment,Book,Action"
            },
            new TimeSlot
            {
                Id = Guid.NewGuid(),
                Name = "Evening Wind Down",
                TypicalDurationMinutes = 45,
                TypicalEnergy = EnergyLevel.Low,
                ActivityTypes = "Article,Video"
            }
        };

        db.TimeSlots.AddRange(defaultSlots);
        await db.SaveChangesAsync();

        Console.WriteLine($"✓ Created {defaultSlots.Length} default time slots:\n");
        foreach (var slot in defaultSlots)
        {
            Console.WriteLine($"  • {slot.Name} ({slot.TypicalDurationMinutes} min, {slot.TypicalEnergy} energy)");
        }

        Console.WriteLine("\nUse 'remedy config slots list' to view all slots");
    }

    private static void ShowConfigHelp()
    {
        Console.WriteLine("Configuration commands:");
        Console.WriteLine();
        Console.WriteLine("  remedy config init              - Initialize with default time slots");
        Console.WriteLine("  remedy config slots list        - List all time slots");
        Console.WriteLine("  remedy config slots add         - Add a new time slot");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  remedy config init");
        Console.WriteLine("  remedy config slots add --name \"Morning\" --duration 60 --energy High");
    }
}
