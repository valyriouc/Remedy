using Remedy.Shared.Data;
using Remedy.Shared.Services;

namespace Remedy.Cli.Commands;

public static class SnoozeCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var id = parser.GetGuidArgument(1);

        if (!id.HasValue)
        {
            Console.WriteLine("Error: Resource ID is required.");
            Console.WriteLine("Usage: remedy snooze <id> [--days <number>]");
            return;
        }

        var days = parser.GetIntOption(3, "--days", "-d");

        await using var db = new RemedyDbContext(Program.DatabaseName);
        await db.Database.EnsureCreatedAsync();

        var resource = await db.Resources.FindAsync(id.Value);

        if (resource == null)
        {
            Console.WriteLine($"Error: Resource with ID {id.Value} not found.");
            return;
        }

        if (resource.IsCompleted)
        {
            Console.WriteLine($"Warning: This resource is already completed.");
            return;
        }

        // Update snooze count and priority
        resource.TimesSnoozed++;
        resource.LastReminded = DateTime.Now.AddDays(days);

        // Apply snooze penalty to priority
        resource.Priority *= 0.9; // Reduce priority by 10%

        // Mark for sync
        var syncService = new SyncService(db);
        syncService.MarkForSync(resource);

        await db.SaveChangesAsync();

        Console.WriteLine($"⏰ Snoozed: {resource.Title}");
        Console.WriteLine($"   Will remind in {days} days ({resource.LastReminded:MMM dd})");
        Console.WriteLine($"   Times snoozed: {resource.TimesSnoozed}");
        Console.WriteLine($"   Priority: {resource.Priority:F2}");

        if (resource.TimesSnoozed >= 3)
        {
            Console.WriteLine($"\n   ⚠️  This resource has been snoozed {resource.TimesSnoozed} times.");
            Console.WriteLine($"   Consider if it's still relevant?");
        }
    }
}
