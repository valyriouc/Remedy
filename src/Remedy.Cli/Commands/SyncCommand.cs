using Remedy.Shared.Data;
using Remedy.Shared.Models;
using Remedy.Shared.Services;

namespace Remedy.Cli.Commands;

public static class SyncCommand
{
    public static async Task ExecuteAsync(CommandParser parser)
    {
        var subCommand = parser.GetSubCommand();

        switch (subCommand)
        {
            case "now":
                await SyncNowAsync();
                break;
            case "status":
                await ShowStatusAsync();
                break;
            case "configure":
                await ConfigureAsync(parser);
                break;
            case "reset":
                await ResetFailedAsync();
                break;
            case "disable":
                await DisableSyncAsync();
                break;
            default:
                ShowHelp();
                break;
        }
    }

    private static async Task SyncNowAsync()
    {
        Console.WriteLine("Starting synchronization...");

        await using var context = new RemedyDbContext(Program.DatabaseName);
        await context.Database.EnsureCreatedAsync();

        var orchestrator = new SyncOrchestrator(context);
        var result = await orchestrator.SynchronizeAsync();

        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {result.Message}");
            Console.ResetColor();

            if (result.PushFailedCount > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Warning: {result.PushFailedCount} items failed to sync");
                Console.ResetColor();
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {result.Message}");
            Console.ResetColor();
        }
    }

    private static async Task ShowStatusAsync()
    {
        var config = SyncConfiguration.Load();

        Console.WriteLine("\n=== Sync Configuration ===");
        Console.WriteLine($"Server URL: {config.ServerUrl ?? "(not configured - offline mode)"}");
        Console.WriteLine($"Auto-sync interval: {(config.AutoSyncIntervalMinutes > 0 ? $"{config.AutoSyncIntervalMinutes} minutes" : "disabled")}");
        Console.WriteLine($"Last sync: {config.LastSyncTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "never"}");
        Console.WriteLine();

        using var context = new RemedyDbContext(Program.DatabaseName);
        await context.Database.EnsureCreatedAsync();

        var orchestrator = new SyncOrchestrator(context);
        var status = await orchestrator.GetSyncStatusAsync();

        Console.WriteLine(status);
    }

    private static async Task ConfigureAsync(CommandParser parser)
    {
        var serverUrl = parser.GetOption("--server") ?? parser.GetOption("-s");
        var intervalStr = parser.GetOption("--interval") ?? parser.GetOption("-i");

        if (string.IsNullOrWhiteSpace(serverUrl) && string.IsNullOrWhiteSpace(intervalStr))
        {
            Console.WriteLine("Usage: remedy sync configure --server <url> [--interval <minutes>]");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  --server, -s   Server base URL (e.g., http://localhost:5000)");
            Console.WriteLine("  --interval, -i Auto-sync interval in minutes (0 to disable)");
            return;
        }

        var config = SyncConfiguration.Load();

        if (!string.IsNullOrWhiteSpace(serverUrl))
        {
            config.ServerUrl = serverUrl.TrimEnd('/');
            Console.WriteLine($"Server URL set to: {config.ServerUrl}");
        }

        if (!string.IsNullOrWhiteSpace(intervalStr) && int.TryParse(intervalStr, out int interval))
        {
            config.AutoSyncIntervalMinutes = interval;
            Console.WriteLine($"Auto-sync interval set to: {(interval > 0 ? $"{interval} minutes" : "disabled")}");
        }

        config.Save();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✓ Configuration saved");
        Console.ResetColor();

        // Try to sync immediately if server is configured
        if (config.IsSyncEnabled())
        {
            Console.WriteLine("\nTesting connection...");
            await SyncNowAsync();
        }
    }

    private static async Task ResetFailedAsync()
    {
        using var context = new RemedyDbContext(Program.DatabaseName);
        await context.Database.EnsureCreatedAsync();

        var syncService = new SyncService(context);
        await syncService.ResetFailedSyncsAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ Reset all failed syncs - they will retry on next sync");
        Console.ResetColor();
    }

    private static async Task DisableSyncAsync()
    {
        var config = SyncConfiguration.Load();
        config.ServerUrl = null;
        config.Save();

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("✓ Sync disabled - running in offline-only mode");
        Console.ResetColor();

        await Task.CompletedTask;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("\nUsage: remedy sync <command> [options]");
        Console.WriteLine("\nCommands:");
        Console.WriteLine("  now              Sync now (push local changes, pull server updates)");
        Console.WriteLine("  status           Show sync status and pending items");
        Console.WriteLine("  configure        Configure sync settings");
        Console.WriteLine("  reset            Reset failed syncs and retry");
        Console.WriteLine("  disable          Disable sync (offline-only mode)");
        Console.WriteLine("\nExamples:");
        Console.WriteLine("  remedy sync now");
        Console.WriteLine("  remedy sync status");
        Console.WriteLine("  remedy sync configure --server http://localhost:5000");
        Console.WriteLine("  remedy sync configure --interval 30");
    }
}
