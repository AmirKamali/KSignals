# ClickHouse Package Version Fix

## Issue
`System.MissingMethodException: Method not found: 'Void ClickHouse.Driver.ADO.ClickHouseConnection.SetFormDataParameters(Boolean)'`

## Root Cause
Version mismatch between `EntityFrameworkCore.ClickHouse` and `ClickHouse.Driver`. The method signature changed between versions.

## Solution
Use the exact versions that are compatible:
- **EntityFrameworkCore.ClickHouse**: 2.0.1
- **ClickHouse.Driver**: 0.7.19 (required by EntityFrameworkCore.ClickHouse 2.0.1)

## Important Notes
1. **Restart Required**: After changing package versions, you MUST:
   - Stop the running application
   - Clean the build: `dotnet clean`
   - Rebuild: `dotnet build`
   - Restart the application

2. **Version Compatibility**:
   - EntityFrameworkCore.ClickHouse 2.0.1 requires ClickHouse.Driver 0.7.19
   - ClickHouse.Driver 0.8.0+ has breaking changes (method removed/changed)
   - Do NOT manually upgrade ClickHouse.Driver beyond 0.7.19 when using EntityFrameworkCore.ClickHouse 2.0.1

3. **Connection String**:
   - Format: `Host=localhost;Port=9000;Database=kalshi_signals;User=...;Password=...`
   - Protocol: Native TCP (port 9000)
   - No `Protocol=` parameter needed for native protocol

## Verification
After restarting the application, the connection should work without the `SetFormDataParameters` error.
