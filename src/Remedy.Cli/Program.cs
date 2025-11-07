using Remedy.Cli.Commands;
using Remedy.Cli.Data;

namespace Remedy.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        // Ensure database is created on first run
        await EnsureDatabaseAsync();

        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var parser = new CommandParser(args);
        var command = parser.GetCommand();

        try
        {
            switch (command)
            {
                case "save":
                    await SaveCommand.ExecuteAsync(parser);
                    break;
                case "list":
                    await ListCommand.ExecuteAsync(parser);
                    break;
                case "start":
                    await StartCommand.ExecuteAsync(parser);
                    break;
                case "done":
                    await DoneCommand.ExecuteAsync(parser);
                    break;
                case "snooze":
                    await SnoozeCommand.ExecuteAsync(parser);
                    break;
                case "config":
                    await ConfigCommand.ExecuteAsync(parser);
                    break;
                case "version":
                    ShowVersion();
                    break;
                case "help":
                case "--help":
                case "-h":
                    ShowHelp();
                    break;
                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Use 'remedy help' for usage information");
                    return 1;
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Remedy CLI - Intelligent resource and reminder management");
        Console.WriteLine();
        Console.WriteLine("Usage: remedy <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  save      Save a new resource");
        Console.WriteLine("  list      List recommended resources");
        Console.WriteLine("  start     Start working on a resource");
        Console.WriteLine("  done      Mark a resource as completed");
        Console.WriteLine("  snooze    Snooze a resource for later");
        Console.WriteLine("  config    Configure time slots and settings");
        Console.WriteLine("  version   Show version information");
        Console.WriteLine("  help      Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  remedy save https://example.com --title \"Great Article\" --type Article");
        Console.WriteLine("  remedy list --energy High --time 60");
        Console.WriteLine("  remedy config init");
    }

    private static void ShowVersion()
    {
        Console.WriteLine("Remedy CLI v1.0.0");
        Console.WriteLine("Intelligent resource and reminder management");
    }

    private static async Task EnsureDatabaseAsync()
    {
        try
        {
            using var db = new RemedyDbContext();
            await db.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not initialize database: {ex.Message}");
        }
    }
}
