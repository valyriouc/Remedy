using System.Diagnostics;
using Remedy.Shared.Data;
using Remedy.Shared.Services;

namespace Remedy.Cli.Commands;

public static class StartCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        Guid? id = parser.GetGuidArgument(1);

        if (!id.HasValue)
        {
            Console.WriteLine("Error: Resource ID is required.");
            Console.WriteLine("Usage: remedy start <id>");
            return;
        }

        await using RemedyDbContext db = new RemedyDbContext();
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
        }

        // Update last reminded
        resource.LastReminded = DateTime.Now;

        // Mark for sync
        var syncService = new SyncService(db);
        syncService.MarkForSync(resource);

        await db.SaveChangesAsync();

        Console.WriteLine($"Starting: {resource.Title}");
        Console.WriteLine($"Type: {resource.Type}");
        Console.WriteLine($"Estimated time: {resource.EstimatedTimeMinutes} minutes");

        if (!string.IsNullOrWhiteSpace(resource.Url))
        {
            Console.WriteLine($"\nOpening: {resource.Url}");

            try
            {
                // Open URL in default browser
                var psi = new ProcessStartInfo
                {
                    FileName = resource.Url,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open URL: {ex.Message}");
                Console.WriteLine($"Please open manually: {resource.Url}");
            }
        }

        Console.WriteLine($"\nWhen finished, use:");
        Console.WriteLine($"  remedy done {id.Value} --rating <1-5>");
    }
}
