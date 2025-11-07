using Microsoft.EntityFrameworkCore;
using Remedy.Cli.Data;
using Remedy.Cli.Models;
using Remedy.Cli.Services;

namespace Remedy.Cli.Commands;

public static class ListCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var count = parser.GetIntOption(3, "--count", "-n");
        var energy = parser.GetOption<EnergyLevel>(EnergyLevel.Medium, "--energy", "-e");
        var time = parser.GetIntOption(30, "--time", "-t");
        var context = parser.GetOption("--context", "-c");
        var slotName = parser.GetOption("--slot", "-s");
        var showAll = parser.HasFlag("--all", "-a");

        using var db = new RemedyDbContext();
        await db.Database.EnsureCreatedAsync();

        if (showAll)
        {
            var allResources = await db.Resources
                .Where(r => !r.IsCompleted)
                .OrderByDescending(r => r.SavedAt)
                .Take(count)
                .ToListAsync();

            if (!allResources.Any())
            {
                Console.WriteLine("No resources found. Use 'remedy save' to add some!");
                return;
            }

            Console.WriteLine($"\nShowing all resources ({allResources.Count}):\n");
            DisplayResources(allResources, showScore: false);
            return;
        }

        // Build user context
        var userContext = new UserContext
        {
            CurrentTime = DateTime.Now,
            CurrentEnergy = energy,
            AvailableDurationMinutes = time,
            CurrentContextDescription = context ?? string.Empty
        };

        // Find active time slot if specified
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            var timeSlot = await db.TimeSlots
                .FirstOrDefaultAsync(ts => ts.Name.ToLower() == slotName.ToLower());

            if (timeSlot != null)
            {
                userContext.ActiveTimeSlotId = timeSlot.Id;
            }
        }

        // Get optimal resources
        var matchingService = new ResourceMatchingService(db);
        var resources = await matchingService.GetOptimalResourcesAsync(userContext, count);

        if (!resources.Any())
        {
            Console.WriteLine("No matching resources found.");
            Console.WriteLine("\nTry:");
            Console.WriteLine("  - Increasing available time (--time)");
            Console.WriteLine("  - Adjusting energy level (--energy)");
            Console.WriteLine("  - Adding more resources (remedy save)");
            Console.WriteLine("  - Using --all to see all resources");
            return;
        }

        Console.WriteLine($"\nTop {resources.Count} recommended resources:\n");
        Console.WriteLine($"Context: Energy={energy}, Time={time}min");
        if (!string.IsNullOrWhiteSpace(context))
        {
            Console.WriteLine($"         {context}");
        }
        Console.WriteLine();

        DisplayResources(resources, showScore: true);

        Console.WriteLine("\nCommands:");
        Console.WriteLine("  remedy start <id>   - Start working on a resource");
        Console.WriteLine("  remedy done <id>    - Mark as completed");
        Console.WriteLine("  remedy snooze <id>  - Snooze for later");
    }

    private static void DisplayResources(List<Resource> resources, bool showScore)
    {
        for (int i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            var daysAgo = (DateTime.Now - resource.SavedAt).Days;

            Console.WriteLine($"{i + 1}. [{resource.Type}] {resource.Title}");
            Console.WriteLine($"   Time: {resource.EstimatedTimeMinutes} min | Difficulty: {resource.Difficulty} | Energy: {resource.MinEnergyLevel}");

            if (daysAgo == 0)
            {
                Console.WriteLine($"   Saved: Today");
            }
            else if (daysAgo == 1)
            {
                Console.WriteLine($"   Saved: Yesterday");
            }
            else
            {
                Console.WriteLine($"   Saved: {daysAgo} days ago");
            }

            if (!string.IsNullOrWhiteSpace(resource.CreatedByContext))
            {
                Console.WriteLine($"   Context: {resource.CreatedByContext}");
            }

            if (!string.IsNullOrWhiteSpace(resource.Url))
            {
                Console.WriteLine($"   URL: {resource.Url}");
            }

            if (showScore)
            {
                Console.WriteLine($"   Match Score: {resource.ComputedScore:F2}");
            }

            Console.WriteLine($"   ID: {resource.Id}");

            if (i < resources.Count - 1)
            {
                Console.WriteLine();
            }
        }
    }
}
