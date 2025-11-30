# ClickHouse Setup - Final Configuration

## ✅ Configuration Complete Per Official Documentation

Following: https://clickhouse.com/docs/integrations/csharp

## Connection String (Correct Format)

```
Host=localhost;Port=8123;Protocol=http;Username=qv6t0lSQNqaaKPEpxazLcZjoS;Password=xXnBtibbHu5be6U1k1X9IBxie;Database=kalshi_signals
```

## Key Configuration Points

1. **Port**: **8123** (HTTP interface)
   - Port 9000 is ONLY for native `clickhouse-client` program
   - ClickHouse.Driver uses HTTP protocol by default

2. **Protocol**: **http** (required parameter)
   - Must be explicitly specified

3. **Username Parameter**: **Username** (not "User")
   - EntityFrameworkCore.ClickHouse uses "Username" parameter

4. **Package Versions**:
   - EntityFrameworkCore.ClickHouse: 2.0.1
   - ClickHouse.Driver: 0.7.19

## Database Status

✅ ClickHouse Server: Running
✅ Database: `kalshi_signals` 
✅ Tables: All 4 tables created and ready
   - market_categories
   - market_snapshots  
   - TagsCategories
   - Users

## ⚠️ RESTART REQUIRED

**The application MUST be restarted** to apply the new connection string configuration.

Current status shows the old error because the running process hasn't reloaded:
```
Port 9000 is for clickhouse-client program
You must use port 8123 for HTTP.
```

## Verification Steps After Restart

1. **Call API**:
   ```bash
   curl -X POST 'http://localhost:3006/api/private/data-source/sync-market-categories' \
     -H 'accept: */*' \
     -d ''
   ```

2. **Wait for processing** (10-20 seconds)

3. **Verify records inserted**:
   ```bash
   docker exec clickhouse-server clickhouse-client \
     --user qv6t0lSQNqaaKPEpxazLcZjoS \
     --password xXnBtibbHu5be6U1k1X9IBxie \
     --query "SELECT count() FROM kalshi_signals.TagsCategories"
   ```

4. **Check actual data**:
   ```bash
   docker exec clickhouse-server clickhouse-client \
     --user qv6t0lSQNqaaKPEpxazLcZjoS \
     --password xXnBtibbHu5be6U1k1X9IBxie \
     --query "SELECT Category, Tag, LastUpdate FROM kalshi_signals.TagsCategories LIMIT 10"
   ```

## Expected Result

After restart, the API should:
- ✅ Accept requests successfully
- ✅ Connect to ClickHouse without port errors
- ✅ Insert records into TagsCategories table
- ✅ Show increasing record counts on subsequent calls
