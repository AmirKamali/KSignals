# ClickHouse Tables Created

All required tables have been successfully created in the ClickHouse database.

## Tables Created

1. **market_categories** - Stores market category information
2. **market_snapshots** - Stores market snapshot data
3. **TagsCategories** - Stores tags and categories
4. **Users** - Stores user information

## Verification

All tables are accessible and ready for use:
- ✅ market_categories (0 rows)
- ✅ market_snapshots (0 rows)
- ✅ TagsCategories (0 rows)
- ✅ Users (0 rows)

## Important Notes

### ID Generation
ClickHouse does **not support auto-increment** IDs. The tables use `Int64` for ID fields:
- `market_snapshots.MarketSnapshotID` - Int64
- `TagsCategories.Id` - Int64
- `Users.Id` - Int64

**You will need to generate IDs manually in your application code** when inserting records. Options include:
1. Use a distributed ID generator (e.g., Snowflake-like algorithm)
2. Use UUIDs converted to Int64
3. Use timestamp-based IDs
4. Query for MAX(Id) + 1 (not recommended for concurrent inserts)

### Data Types
- String fields use `String` type
- Numeric fields use appropriate types (Int32, Int64, Decimal64, Float64)
- Boolean fields use `UInt8` (0/1)
- DateTime fields use `DateTime`
- Nullable fields use `Nullable(Type)`

### Table Engines
All tables use the `MergeTree` engine, which is optimal for analytical workloads and supports:
- Fast inserts
- Efficient queries
- Data compression
- Partitioning capabilities

## Connection Details

- **Host**: localhost
- **Port**: 9000 (Native), 8123 (HTTP)
- **Database**: kalshi_signals
- **Username**: qv6t0lSQNqaaKPEpxazLcZjoS
- **Password**: xXnBtibbHu5be6U1k1X9IBxie

## Next Steps

1. Update application code to handle manual ID generation
2. Test insert operations
3. Consider implementing a distributed ID generator for production use
4. Set up proper indexing if needed for query performance
