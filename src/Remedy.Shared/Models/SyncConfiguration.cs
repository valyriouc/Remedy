using System.Text.Json;

namespace Remedy.Shared.Models;

/// <summary>
/// Configuration for sync behavior
/// </summary>
public class SyncConfiguration
{
    /// <summary>
    /// Server base URL (null means offline-only mode)
    /// </summary>
    public string? ServerUrl { get; set; }

    /// <summary>
    /// Auto-sync interval in minutes (0 means disabled)
    /// </summary>
    public int AutoSyncIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of retry attempts for failed syncs
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial retry delay in milliseconds
    /// </summary>
    public int InitialRetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Last successful sync timestamp
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

    /// <summary>
    /// Enable debug logging for sync operations
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    private static string GetConfigPath()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var remedyFolder = Path.Combine(folder, "Remedy");
        Directory.CreateDirectory(remedyFolder);
        return Path.Combine(remedyFolder, "sync-config.json");
    }

    /// <summary>
    /// Loads configuration from disk
    /// </summary>
    public static SyncConfiguration Load()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            var defaultConfig = new SyncConfiguration();
            defaultConfig.Save();
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<SyncConfiguration>(json) ?? new SyncConfiguration();
        }
        catch
        {
            return new SyncConfiguration();
        }
    }

    /// <summary>
    /// Saves configuration to disk
    /// </summary>
    public void Save()
    {
        var configPath = GetConfigPath();

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(configPath, json);
    }

    /// <summary>
    /// Checks if sync is enabled (server URL is configured)
    /// </summary>
    public bool IsSyncEnabled()
    {
        return !string.IsNullOrWhiteSpace(ServerUrl);
    }
}
