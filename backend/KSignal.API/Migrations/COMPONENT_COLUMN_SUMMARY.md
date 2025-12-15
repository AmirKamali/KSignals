# Component Column Migration Summary

## Overview
Added automatic component/class name tracking to sync_logs table using C# CallerFilePath attribute.

## Database Changes

### New Column
- **Name**: `Component`
- **Type**: `String`
- **Max Length**: 255
- **Required**: Yes
- **Default**: Empty string

### Migration Script
Run: `clickhouse-client < add_component_column_to_sync_logs.sql`

Or directly:
```bash
clickhouse-client --query="ALTER TABLE kalshi_signals.sync_logs ADD COLUMN Component String DEFAULT '';"
```

## Code Changes

### 1. SyncLog Model
- Added `Component` property to track the calling class/component

### 2. ISyncLogService Interface
- Added `[CallerFilePath]` parameter to automatically capture caller's file path
- No changes required to existing calls - backward compatible

### 3. SyncLogService Implementation
- Added `ExtractComponentName()` method to parse file path
- Automatically extracts class name from file path
- Sets component to "Unknown" if extraction fails

## How It Works

The `[CallerFilePath]` attribute is a C# compiler service that automatically populates the parameter with the full file path of the calling code at compile time.

### Example Call
```csharp
// In EventsController.cs
await _syncLogService.LogSyncEventAsync("FetchingEvents", 10, cancellationToken, LogType.Info);
```

### What Gets Logged
- **EventName**: "FetchingEvents"
- **NumbersEnqueued**: 10
- **Type**: "Info"
- **Component**: "EventsController" (automatically extracted from file path)
- **LogDate**: Current UTC timestamp

## Component Name Extraction

The service extracts the component name from the file path:
- `/path/to/EventsController.cs` → `EventsController`
- `/path/to/SynchronizationService.cs` → `SynchronizationService`
- `/path/to/CleanupService.cs` → `CleanupService`

## Benefits

✅ **Zero-touch integration** - Existing code works without changes
✅ **Automatic tracking** - No manual component name passing required
✅ **Type-safe** - Compiler handles the file path capture
✅ **Easy debugging** - Quickly identify which component generated a log
✅ **Performance** - Extraction happens at compile time (CallerFilePath)
✅ **Backward compatible** - All existing calls work as-is

## Example Log Entries

| EventName | Component | Type | NumbersEnqueued |
|-----------|-----------|------|-----------------|
| EventsController_FetchingTagsByCategories | EventsController | Info | 1 |
| CleanupMarketData_QueueingStarted | CleanupService | Info | 150 |
| SynchronizeEventDetail | SynchronizeEventDetailConsumer | Info | 25 |
| CleanupTicker_SqlExecutionError | CleanupService | WARN | 0 |

## Querying Logs by Component

```sql
-- Get all logs from EventsController
SELECT * FROM kalshi_signals.sync_logs WHERE Component = 'EventsController';

-- Get error logs by component
SELECT Component, COUNT(*) as ErrorCount
FROM kalshi_signals.sync_logs
WHERE Type = 'ERROR'
GROUP BY Component
ORDER BY ErrorCount DESC;

-- Get recent activity by component
SELECT Component, EventName, Type, NumbersEnqueued, LogDate
FROM kalshi_signals.sync_logs
ORDER BY LogDate DESC
LIMIT 100;
```
