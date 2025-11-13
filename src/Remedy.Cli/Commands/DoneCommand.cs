using Microsoft.EntityFrameworkCore;
using Remedy.Shared.Data;
using Remedy.Shared.Models;
using Remedy.Shared.Services;

namespace Remedy.Cli.Commands;

public static class DoneCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var id = parser.GetGuidArgument(1);

        if (!id.HasValue)
        {
            Console.WriteLine("Error: Resource ID is required.");
            Console.WriteLine("Usage: remedy done <id> [--rating <1-5>]");
            return;
        }

        int rating = parser.GetIntOption(0, "--rating", "-r");

        if (rating < 0) rating = 0;
        if (rating > 5) rating = 5;

        await using var db = new RemedyDbContext(Program.DatabaseName);
        await db.Database.EnsureCreatedAsync();

        Resource? resource = await db.Resources.FindAsync(id.Value);

        if (resource == null)
        {
            Console.WriteLine($"Error: Resource with ID {id.Value} not found.");
            return;
        }

        // If no rating provided, ask for it
        if (rating == 0)
        {
            Console.Write("Rate this resource (1-5, or press Enter to skip): ");
            var input = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(input) && int.TryParse(input, out int r) && r >= 1 && r <= 5)
            {
                rating = r;
            }
        }

        // Update resource
        resource.IsCompleted = true;
        resource.CompletedAt = DateTime.Now;

        if (rating > 0)
        {
            resource.Rating = rating;
            resource.RelevanceScore = rating / 5.0;
        }

        // Mark for sync
        var syncService = new SyncService(db);
        syncService.MarkForSync(resource);

        await db.SaveChangesAsync();

        Console.WriteLine($"✓ Completed: {resource.Title}");
        if (rating > 0)
        {
            Console.WriteLine($"  Rating: {new string('★', rating)}{new string('☆', 5 - rating)}");
        }

        // Show stats
        int totalCompleted = await db.Resources.CountAsync(r => r.IsCompleted);
        var avgRating = await db.Resources
            .Where(r => r.IsCompleted && r.Rating.HasValue)
            .Select(r => r.Rating!.Value)
            .AverageAsync();
        
        Console.WriteLine($"\nStats: {totalCompleted} resources completed");
        if (avgRating > 0)
        {
            Console.WriteLine($"       Average rating: {avgRating:F1}★");
        }
    }
}
