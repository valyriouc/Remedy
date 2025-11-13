# Remedy Offline-First Client-Server Architecture

## Overview

Remedy has been migrated to a **client-server architecture with offline-first capabilities**. The CLI client works fully offline by default and automatically syncs with the server when available.

## Architecture Components

### 1. Shared Layer (Remedy.Shared)
- **Models**: `Resource`, `TimeSlot` now inherit from `SyncableEntity` with sync tracking fields
  - `SyncStatus`: Tracks sync state (LocalOnly, PendingSync, Synced, SyncFailed)
  - `ServerId`: Server-assigned ID for conflict resolution
  - `ModifiedAt`: Timestamp for change tracking
  - `Version`: Version number for optimistic concurrency
  - `IsDeleted`: Soft delete flag

- **Data**: `RemedyDbContext` moved to shared layer, used by both CLI and Server

- **Services**:
  - `SyncService`: Manages offline-first operations, marks entities for sync
  - `HttpSyncClient`: HTTP client with retry logic and exponential backoff
  - `SyncOrchestrator`: Coordinates full sync workflow (push/pull)

- **DTOs**: Data transfer objects for API communication

### 2. CLI Client (Remedy.Cli)
- **Offline-first**: All operations work without server connection
- **Automatic sync tracking**: Every create/update operation marks entities for sync
- **Sync command**: Manual sync trigger with status reporting
- **Updated commands**: All commands (save, done, snooze, start, config) now mark entities for sync

### 3. Server (Remedy.Server)
- **ASP.NET Core Web API** with Swagger documentation
- **REST endpoints**:
  - `/api/resources` - Resource CRUD with sync support
  - `/api/slots` - Time slot CRUD with sync support
  - `/api/sync/batch` - Batch sync endpoint
  - `/api/sync/pull` - Pull server changes
  - `/api/sync/health` - Health check

- **Conflict detection**: Version-based optimistic concurrency
- **Soft deletes**: Deleted items synced before permanent removal

## Getting Started

### 1. Run the Server (Optional)

The server is optional - CLI works fully offline without it.

```bash
cd src/Remedy.Server
dotnet run
```

Server will start on `http://localhost:5000` by default.
Swagger UI available at: `http://localhost:5000/swagger`

### 2. Configure CLI for Sync

By default, CLI runs in **offline-only mode**. To enable sync:

```bash
# Configure server URL
remedy sync configure --server http://localhost:5000

# Optionally set auto-sync interval (not yet implemented in background worker)
remedy sync configure --interval 30
```

Configuration is saved to: `%LocalApplicationData%\Remedy\sync-config.json`

### 3. Use the CLI (Works Offline!)

All CLI commands work exactly as before, but now with sync tracking:

```bash
# Save a resource (works offline, marked for sync)
remedy save https://example.com --title "Article" --type Article

# List resources
remedy list

# Complete a resource
remedy done <id> --rating 5

# All changes are tracked for sync
```

### 4. Synchronize with Server

```bash
# Sync now (push local changes, pull server updates)
remedy sync now

# Check sync status
remedy sync status

# Reset failed syncs (retry)
remedy sync reset

# Disable sync (back to offline-only)
remedy sync disable
```

## Offline-First Workflow

### Creating/Updating Data
1. User performs action (save, done, snooze, etc.)
2. CLI saves to local SQLite database
3. Entity marked as `PendingSync`
4. User sees immediate success (no waiting for server)
5. Next sync will push changes to server

### Synchronization Process
1. **Push phase**: Send pending local changes to server
   - Batch sync for efficiency
   - Retry with exponential backoff (3 attempts)
   - Conflict detection (version-based)

2. **Pull phase**: Get server changes since last sync
   - Download new/updated entities
   - Apply changes to local database
   - Skip conflicts (local changes preserved)

3. **Cleanup**: Purge entities deleted on both client and server

### Server Unavailable?
- CLI continues working offline
- Changes queued for sync
- Automatic retry on next sync attempt
- No data loss - everything stored locally

## Sync Status Tracking

### Entity States
- **LocalOnly**: Never synced, newly created
- **PendingSync**: Local changes waiting to sync
- **Synced**: Up to date with server
- **SyncFailed**: Sync attempted but failed (will retry)

### Checking Status
```bash
remedy sync status
```

Shows:
- Server URL
- Last sync time
- Number of pending items
- Failed items (if any)

## Conflict Resolution

### How Conflicts Are Detected
- Client and server both have changes to same entity
- Version numbers don't match
- ModifiedAt timestamps differ

### Resolution Strategy
- **Server wins for pulls**: If server has newer version during pull, keep local changes as pending
- **Version check for pushes**: Server rejects pushes with version conflicts
- **Manual resolution**: User can retry after reviewing conflicts

## API Documentation

With server running, visit: `http://localhost:5000/swagger`

### Key Endpoints

**Resources:**
- `GET /api/resources` - List all resources (with optional ?modifiedSince filter)
- `GET /api/resources/{id}` - Get specific resource
- `POST /api/resources` - Create or update resource
- `DELETE /api/resources/{id}` - Soft delete resource

**Time Slots:**
- `GET /api/slots` - List all time slots
- `GET /api/slots/{id}` - Get specific time slot
- `POST /api/slots` - Create or update time slot
- `DELETE /api/slots/{id}` - Soft delete time slot

**Sync:**
- `POST /api/sync/batch` - Batch sync (push multiple entities)
- `GET /api/sync/pull?since=<timestamp>` - Pull changes since timestamp
- `GET /api/sync/health` - Check server health

## Database Schema Changes

New fields added to all entities:

```sql
-- Sync tracking fields
SyncStatus INTEGER DEFAULT 0  -- Enum: LocalOnly, PendingSync, Synced, SyncFailed
ServerId TEXT                  -- Server's ID for this entity
LastSyncedAt DATETIME          -- When last successfully synced
ModifiedAt DATETIME            -- When last modified locally
IsDeleted INTEGER DEFAULT 0    -- Soft delete flag
Version INTEGER DEFAULT 1      -- Version for conflict detection
SyncRetryCount INTEGER DEFAULT 0
LastSyncError TEXT
```

Indexes added for efficient sync queries:
- `(SyncStatus)`
- `(SyncStatus, ModifiedAt)`

## Testing Offline-First Behavior

### Test Scenario 1: Pure Offline
```bash
# Don't configure server
remedy save https://example.com --title "Test"
remedy list
remedy sync status  # Shows "offline-only mode"
```

### Test Scenario 2: Offline-then-Online
```bash
# Work offline
remedy save https://example.com --title "Offline Test"
remedy save https://example2.com --title "Another One"

# Configure server
remedy sync configure --server http://localhost:5000

# Sync (pushes 2 items)
remedy sync now
```

### Test Scenario 3: Server Unavailable
```bash
# Configure server
remedy sync configure --server http://localhost:5000

# Stop server (Ctrl+C in server terminal)

# CLI still works!
remedy save https://example.com --title "Test"
remedy sync now  # Shows "Server unavailable" but data is safe

# Restart server
cd src/Remedy.Server && dotnet run

# Retry sync
remedy sync now  # Successfully syncs pending items
```

### Test Scenario 4: Conflict Handling
```bash
# On CLI 1: Create resource
remedy save https://example.com --title "Test"
remedy sync now

# On CLI 2: Get the resource, modify it
remedy sync now  # Pull from server
remedy done <id> --rating 5
remedy sync now  # Push to server

# On CLI 1: Modify same resource
remedy done <id> --rating 3
remedy sync now  # Conflict detected, local change preserved
```

## Data Location

- **CLI Database**: `%LocalApplicationData%\Remedy\remedy.db`
- **CLI Config**: `%LocalApplicationData%\Remedy\sync-config.json`
- **Server Database**: `%LocalApplicationData%\Remedy\remedy.db` (same file, shared)

> **Note**: Currently both CLI and Server use the same database file. For production multi-client scenarios, configure the server to use a separate database path.

## Architecture Benefits

1. **Always Available**: CLI works without internet connection
2. **No Waiting**: Immediate feedback on all operations
3. **Reliable**: Changes never lost, queued for sync
4. **Resilient**: Automatic retry with exponential backoff
5. **Efficient**: Batch syncing reduces network overhead
6. **Safe**: Conflict detection prevents data loss
7. **Flexible**: Can disable sync entirely for offline-only use

## Future Enhancements

Potential improvements:

1. **Background Sync Worker**: Automatic sync on a schedule
2. **Conflict UI**: Better visualization and resolution of conflicts
3. **Multi-device**: Better handling of multiple clients syncing same data
4. **Push Notifications**: Server notifies clients of changes
5. **Selective Sync**: Sync only specific resources or time slots
6. **Compression**: Compress large sync payloads
7. **Delta Sync**: Send only changed fields, not full entities

## Troubleshooting

### Sync Fails Repeatedly
```bash
# Check status
remedy sync status

# Reset failed syncs
remedy sync reset

# Try again
remedy sync now
```

### Lost Connection to Server
- CLI continues working offline
- Changes queued for next sync
- No intervention needed

### Conflicts
- Local changes preserved
- Review with `remedy list`
- Manually choose to keep or discard
- Re-sync after resolution

### Clear Sync State (Nuclear Option)
```bash
# Disable sync
remedy sync disable

# Delete database (WARNING: loses all data)
# rm %LocalApplicationData%\Remedy\remedy.db

# Reconfigure
remedy sync configure --server http://localhost:5000
remedy sync now  # Pull fresh from server
```

## Migration Notes

### Breaking Changes
- Database schema changed (new sync fields added)
- Old databases will be updated automatically via `EnsureCreated()`
- No data migration needed (new fields have defaults)

### Backwards Compatibility
- CLI commands unchanged (same syntax)
- Offline functionality identical to before
- New `sync` command is optional

### API Versioning
- Current API version: 1.0
- Breaking changes will increment version
- Version negotiation not yet implemented

## Summary

The Remedy offline-first architecture provides:
- **Client Independence**: CLI works perfectly offline
- **Server Optional**: Enable sync only when needed
- **Automatic Tracking**: Every change marked for sync
- **Robust Syncing**: Retry, conflict detection, batch processing
- **Zero Data Loss**: All changes preserved locally first

Use `remedy sync --help` for more sync command options.
