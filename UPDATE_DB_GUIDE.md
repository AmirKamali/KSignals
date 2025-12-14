# Database Update Guide

This guide provides instructions for connecting to and updating the ClickHouse database used by the Kalshi Signals application.

## ⚠️ CRITICAL: Database Update Policy

**🚨 NEVER UPDATE THE LOCAL DATABASE 🚨**

**All database changes MUST be applied to the remote production database only (`kalshisignals.com`).**

- **DO NOT** modify the local development database
- **DO NOT** use `localhost` for any updates, schema changes, or data modifications
- **ALWAYS** use `kalshisignals.com` as the host for all database operations
- The local database is for read-only queries and testing only

**Why?** The local database is not synchronized with production and changes made locally will not affect the live application. All schema changes, data updates, and migrations must be applied directly to the remote production database.

## Quick Start

**Recommended method** - Use local Docker ClickHouse client to connect to remote database:

```bash
# 1. Ensure local ClickHouse Docker container is running
cd clickHouse && docker compose up -d

# 2. Connect to remote database and execute query
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "SELECT * FROM kalshi_signals.Users LIMIT 10"
```

⚠️ **Always verify `--host kalshisignals.com` is specified** to ensure you're connecting to the remote production database.

## Connection Details

### Production Database
- **Host**: `kalshisignals.com`
- **Port**: `8123` (HTTP interface) or `9000` (Native protocol)
- **Database**: `kalshi_signals`
- **Username**: `qv6t0lSQNqaaKPEpxazLcZjoS`
- **Password**: `xXnBtibbHu5be6U1k1X9IBxie`

### Local Development Database
- **Host**: `localhost` (when running via Docker)
- **Port**: `8123` (HTTP interface) or `9000` (Native protocol)
- **Database**: `kalshi_signals`
- **Username**: `qv6t0lSQNqaaKPEpxazLcZjoS`
- **Password**: `xXnBtibbHu5be6U1k1X9IBxie`

## Connection Methods

### 1. ⭐ Recommended: Using Local Docker ClickHouse Client (Connecting to Remote Database)

**This is the recommended method** for connecting to and updating the remote production database. Use the `clickhouse-client` from your local Docker container to connect to the remote database at `kalshisignals.com`.

**Prerequisites:**
1. Start the local ClickHouse Docker container (if not already running):
   ```bash
   cd clickHouse
   docker compose up -d
   ```
2. Verify the container is running:
   ```bash
   docker ps | grep clickhouse-server
   ```
3. The container provides the `clickhouse-client` tool which you'll use to connect to the **remote** database

**Basic format:**
```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "YOUR_SQL_QUERY"
```

**Example - Query:**
```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "SELECT * FROM kalshi_signals.Users LIMIT 10 FORMAT PrettyCompact"
```

**Example - Update:**
```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "ALTER TABLE kalshi_signals.Users UPDATE Username = 'newusername' WHERE FirebaseId = 'firebase-id-123'"
```

**Interactive Mode:**
To open an interactive session:
```bash
docker exec -it clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals
```

⚠️ **IMPORTANT**: Always specify `--host kalshisignals.com` to ensure you're connecting to the remote production database, not the local one.

### 2. Using cURL (HTTP Interface)

Alternative method using HTTP interface for quick queries and updates.

**Basic format:**
```bash
curl -u "USERNAME:PASSWORD" \
  "http://kalshisignals.com:8123/?database=kalshi_signals" \
  --data-binary "YOUR_SQL_QUERY"
```

**Example - Query:**
```bash
curl -u "qv6t0lSQNqaaKPEpxazLcZjoS:xXnBtibbHu5be6U1k1X9IBxie" \
  "http://kalshisignals.com:8123/?database=kalshi_signals" \
  --data-binary "SELECT * FROM kalshi_signals.Users LIMIT 10 FORMAT PrettyCompact"
```

**Example - Update:**
```bash
curl -u "qv6t0lSQNqaaKPEpxazLcZjoS:xXnBtibbHu5be6U1k1X9IBxie" \
  "http://kalshisignals.com:8123/?database=kalshi_signals" \
  --data-binary "ALTER TABLE kalshi_signals.Users UPDATE Username = 'newusername' WHERE FirebaseId = 'firebase-id-123'"
```

### 3. Using ClickHouse Client (If Installed Locally)

If you have `clickhouse-client` installed directly on your system (not via Docker):

```bash
clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "YOUR_SQL_QUERY"
```

⚠️ **WARNING**: If you use this method without specifying `--host kalshisignals.com`, you may accidentally connect to a local instance. Always explicitly specify the remote host.

### 4. Using Application Connection String

The application uses the following connection string format:

```
Host=kalshisignals.com;Port=8123;Database=kalshi_signals;Username=qv6t0lSQNqaaKPEpxazLcZjoS;Password=xXnBtibbHu5be6U1k1X9IBxie
```

This can be configured via:
- **appsettings.json**: `ConnectionStrings:KalshiClickHouse`
- **Environment Variables**:
  - `KALSHI_DB_HOST` (default: `kalshisignals.com`)
  - `KALSHI_DB_PORT` (default: `8123`)
  - `KALSHI_DB_USER`
  - `KALSHI_DB_PASSWORD`
  - `KALSHI_DB_NAME` (default: `kalshi_signals`)
  - `KALSHI_DB_CONNECTION` (full connection string override)

## Common Operations

All examples below use the recommended method: **local Docker ClickHouse client connecting to remote database**. Always ensure `--host kalshisignals.com` is specified.

### View Table Structure

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "DESCRIBE TABLE kalshi_signals.Users"
```

### Query Data

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "SELECT * FROM kalshi_signals.Users FORMAT PrettyCompact"
```

### Insert Data

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "INSERT INTO kalshi_signals.Users (FirebaseId, CreatedAt, UpdatedAt) VALUES ('firebase-id-123', now(), now())"
```

### Update Data

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "ALTER TABLE kalshi_signals.Users UPDATE Username = 'newusername' WHERE FirebaseId = 'firebase-id-123'"
```

### Delete Data

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "ALTER TABLE kalshi_signals.Users DELETE WHERE FirebaseId = 'firebase-id-123'"
```

### Drop Table

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "DROP TABLE IF EXISTS kalshi_signals.Users"
```

### Create Table

```bash
docker exec clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals \
  --query "CREATE TABLE kalshi_signals.Users (
    Id UUID DEFAULT generateUUIDv4(),
    FirebaseId String NOT NULL,
    Username Nullable(String),
    FirstName Nullable(String),
    LastName Nullable(String),
    Email Nullable(String),
    IsComnEmailOn UInt8 DEFAULT 0,
    StripeCustomerId Nullable(String),
    ActiveSubscriptionId Nullable(UUID),
    ActivePlanId Nullable(UUID),
    SubscriptionStatus String DEFAULT 'none',
    CreatedAt DateTime NOT NULL,
    UpdatedAt DateTime NOT NULL
) ENGINE = MergeTree ORDER BY Id"
```

### Multi-line Queries

For complex queries, you can use a here-document or save to a file:

**Using here-document:**
```bash
docker exec -i clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals <<EOF
SELECT 
  Id,
  FirebaseId,
  Username,
  CreatedAt
FROM kalshi_signals.Users
WHERE CreatedAt > '2024-01-01'
ORDER BY CreatedAt DESC
LIMIT 100
EOF
```

**Using a SQL file:**
```bash
docker exec -i clickhouse-server clickhouse-client \
  --host kalshisignals.com \
  --port 9000 \
  --user qv6t0lSQNqaaKPEpxazLcZjoS \
  --password xXnBtibbHu5be6U1k1X9IBxie \
  --database kalshi_signals < query.sql
```

## Important Notes

### UUID Auto-Generation

The `Users` table uses UUID for the `Id` column with auto-generation:
- `Id UUID DEFAULT generateUUIDv4()`
- When inserting, you can omit the `Id` column and it will be auto-generated
- The application's EF Core configuration also handles UUID generation client-side

### ClickHouse-Specific Behaviors

1. **ALTER TABLE for Updates/Deletes**: ClickHouse uses `ALTER TABLE ... UPDATE` and `ALTER TABLE ... DELETE` instead of standard SQL `UPDATE` and `DELETE` statements. These operations are asynchronous.

2. **No RETURNING Clause**: ClickHouse doesn't support `RETURNING` clause, so the application generates UUIDs client-side before insert.

3. **Format Options**: When querying via HTTP, you can specify output format:
   - `FORMAT PrettyCompact` - Human-readable table
   - `FORMAT JSON` - JSON output
   - `FORMAT CSV` - CSV output
   - `FORMAT TabSeparated` - Tab-separated values

### Safety Warnings

⚠️ **WARNING**: Always be cautious when modifying production database:

1. **NEVER UPDATE LOCAL**: All changes must be applied to the remote database (`kalshisignals.com`) only. Do not modify the local database.
2. **Backup First**: Consider backing up data before major changes
3. **Verify Queries**: Double-check SQL queries before executing - especially verify you're connecting to `kalshisignals.com`, not `localhost`
4. **Use Transactions**: When possible, use transactions for multiple related changes
5. **Monitor Impact**: ClickHouse ALTER operations are asynchronous - monitor for completion
6. **Double-Check Host**: Always verify the host in your connection string is `kalshisignals.com` before executing any write operations

### Environment-Specific Configuration

The application automatically selects the correct database based on environment variables:

- **Production**: Uses `kalshisignals.com` (default) - **USE THIS FOR ALL UPDATES**
- **Local Development**: Set `KALSHI_DB_HOST=localhost` for local Docker instance - **READ-ONLY ONLY, NO UPDATES**
- **Override**: Use `KALSHI_DB_CONNECTION` for full connection string override

**⚠️ Remember**: When making database changes, ensure you're connecting to `kalshisignals.com`, not `localhost`. The local database should never be modified.

## Troubleshooting

### Connection Issues

1. **Check host accessibility**: Ensure `kalshisignals.com` is reachable
2. **Verify credentials**: Double-check username and password
3. **Port availability**: Ensure ports 8123 (HTTP) or 9000 (Native) are accessible
4. **Firewall**: Check if firewall rules allow connection

### Query Errors

1. **Table not found**: Ensure you're using the correct database name (`kalshi_signals`)
2. **Syntax errors**: ClickHouse SQL syntax may differ from standard SQL
3. **Type mismatches**: Verify data types match table schema

## Additional Resources

- [ClickHouse Documentation](https://clickhouse.com/docs)
- [ClickHouse HTTP Interface](https://clickhouse.com/docs/en/interfaces/http)
- [ClickHouse SQL Reference](https://clickhouse.com/docs/en/sql-reference)

