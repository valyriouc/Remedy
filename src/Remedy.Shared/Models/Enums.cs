namespace Remedy.Shared.Models;

public enum ResourceType
{
    Video,
    Article,
    Book,
    Action,
    Experiment,
    Event
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard
}

public enum TargetTimeframe
{
    Today,
    ThisWeek,
    ThisMonth,
    Someday
}

public enum EnergyLevel
{
    Low,
    Medium,
    High
}

public enum SyncStatus
{
    /// <summary>
    /// Entity is synchronized with server
    /// </summary>
    Synced,

    /// <summary>
    /// Entity has local changes pending sync
    /// </summary>
    PendingSync,

    /// <summary>
    /// Sync failed, will retry
    /// </summary>
    SyncFailed,

    /// <summary>
    /// Entity only exists locally (never synced)
    /// </summary>
    LocalOnly
}
