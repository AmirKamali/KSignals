# ClickHouse Connection Fix

## Issue
The connection string format was incorrect for EntityFrameworkCore.ClickHouse 2.0.1.

## Solution
Updated the connection string to use the correct key-value pair format instead of URI format.

### Correct Format
```
Host=localhost;Port=8123;Database=kalshi_signals;User=qv6t0lSQNqaaKPEpxazLcZjoS;Password=xXnBtibbHu5be6U1k1X9IBxie;Protocol=http
```

### Changes Made
1. **Connection String Format**: Changed from `clickhouse://user:pass@host:port/db` to `Host=...;Port=...;Database=...;User=...;Password=...;Protocol=http`
2. **Default Port**: Changed from 9000 (native protocol) to 8123 (HTTP protocol) to match the Protocol=http setting
3. **Updated Files**:
   - `Program.cs` - BuildConnectionString method
   - `appsettings.json` - Connection string
   - `appsettings.Development.json` - Connection string

## Port Information
- **Port 8123**: HTTP interface (used by EntityFrameworkCore.ClickHouse with Protocol=http)
- **Port 9000**: Native protocol (used for direct ClickHouse client connections)

## Testing
After restarting the API, the connection should work correctly. The endpoint will:
1. Accept the request (202 Accepted)
2. Queue the synchronization message
3. Process it in the background consumer
4. Successfully connect to ClickHouse and perform database operations
