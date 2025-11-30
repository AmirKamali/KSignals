# ClickHouse Setup - Complete Configuration

## ✅ Configuration Complete

The connection string has been updated according to the official ClickHouse C# documentation:
https://clickhouse.com/docs/integrations/csharp

## Connection String Format

**Correct Format (HTTP Protocol):**
```
Host=localhost;Port=8123;Protocol=http;Username=qv6t0lSQNqaaKPEpxazLcZjoS;Password=xXnBtibbHu5be6U1k1X9IBxie;Database=kalshi_signals
```

## Important Points from Official Documentation

1. **ClickHouse.Driver uses HTTP by default**
   - Requires port **8123** (not 9000)
   - Port 9000 is for native `clickhouse-client` program only
   - Must specify `Protocol=http` in connection string

2. **Connection String Parameters:**
   - `Host`: ClickHouse server address
   - `Port`: **8123** for HTTP (default)
   - `Protocol`: **http** (required)
   - `Username`: Authentication username (note: "Username" not "User")
   - `Password`: Authentication password
   - `Database`: Target database name

3. **Package Versions:**
   - `EntityFrameworkCore.ClickHouse`: 2.0.1
   - `ClickHouse.Driver`: 0.7.19 (required dependency)

## Database Status

✅ **ClickHouse Server**: Running
- Port 8123: HTTP interface (used by ClickHouse.Driver)
- Port 9000: Native protocol (for clickhouse-client only)

✅ **Database**: `kalshi_signals` exists
✅ **Tables Created**: All 4 tables exist
  - `market_categories`
  - `market_snapshots`
  - `TagsCategories`
  - `Users`

## ⚠️ CRITICAL: Restart Required

**The application MUST be restarted** for the connection string changes to take effect.

The current running application is still using the old configuration (port 9000), which is why you see the error:
```
Port 9000 is for clickhouse-client program
You must use port 8123 for HTTP.
```

## Steps to Apply Changes

1. **Stop the running application**
2. **Rebuild** (already done - connection string updated)
3. **Restart the application**
4. **Test the API**:
   ```bash
   curl -X POST 'http://localhost:3006/api/private/data-source/sync-market-categories' \
     -H 'accept: */*' \
     -d ''
   ```
5. **Verify records inserted**:
   ```bash
   docker exec clickhouse-server clickhouse-client \
     --user qv6t0lSQNqaaKPEpxazLcZjoS \
     --password xXnBtibbHu5be6U1k1X9IBxie \
     --query "SELECT count() FROM kalshi_signals.TagsCategories"
   ```

## Expected Result After Restart

- ✅ API accepts requests (202 Accepted)
- ✅ Background consumer processes messages
- ✅ Database connection succeeds (no port errors)
- ✅ Records are inserted into `TagsCategories` table
- ✅ Data sync completes successfully
