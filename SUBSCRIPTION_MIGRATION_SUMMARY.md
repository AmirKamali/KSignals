# Subscription Tables Migration to Use User.Id

## Date: 2025-12-13

## Summary
Updated all subscription-related tables and code to use `User.Id` (UUID) instead of `FirebaseId` as the foreign key reference.

## Database Changes

### Tables Updated/Created

1. **subscription_plans** - CREATED
   - `Id`: UUID (auto-generated)
   - No user reference (standalone table)

2. **user_subscriptions** - RECREATED
   - `Id`: UUID (auto-generated)
   - `UserId`: UUID ← Changed from `FirebaseId` (String)
   - `PlanId`: UUID ← Changed from String
   - Foreign key to Users(Id)
   - Foreign key to subscription_plans(Id)

3. **subscription_events** - CREATED
   - `Id`: UUID (auto-generated)
   - `UserId`: UUID ← Changed from `FirebaseId` (String)
   - `SubscriptionId`: Nullable UUID ← Changed from Nullable String
   - Foreign key to Users(Id)
   - Foreign key to user_subscriptions(Id)

## Model Changes

### UserSubscription
- `Id`: string → Guid
- `FirebaseId`: string → REMOVED
- `UserId`: ADDED (Guid, Required)
- `PlanId`: string → Guid
- Added navigation properties: `User`, `Plan`

### SubscriptionEventLog
- `Id`: string → Guid
- `FirebaseId`: string → REMOVED
- `UserId`: ADDED (Guid, Required)
- `SubscriptionId`: string? → Guid?
- Added navigation properties: `User`, `Subscription`

### SubscriptionPlan
- `Id`: string → Guid
- No other changes

## DbContext Changes

All three entities now use:
- `ValueGeneratedOnAdd()` for Id
- `HasDefaultValueSql("generateUUIDv4()")` for database-side generation
- Proper foreign key relationships configured
- Appropriate indexes on UserId, PlanId, StripeSubscriptionId

## Service & Controller Changes Needed

### Remaining Compilation Errors to Fix:

The following files need updates to handle Guid/string conversions:

1. **StripeSubscriptionService.cs**:
   - Line 140: user.ActiveSubscriptionId (string) → need Guid.Parse() or null handling
   - Line 158, 166: plan.Id (Guid) comparisons with user.ActivePlanId (string)
   - Line 328: user.ActiveSubscriptionId (string) → Guid? conversion
   - Line 370, 425: Guid/string comparisons
   - Line 488-489: string.HasValue/Value → null/empty string checks
   - Line 608, 623: string → Guid conversions

2. **UsersController.cs**:
   - Line 36, 43: Guid == string comparisons
   - Line 59, 77: Guid? ?? string - need .ToString()

3. **SubscriptionsController.cs**:
   - Line 76: FirebaseId → UserId
   - Line 220, 280, 292, 304: Guid/string conversions
   - Line 346, 353: Guid == string comparisons

## Key Patterns for Fixes

### When User.ActiveSubscriptionId/ActivePlanId are strings:
```csharp
// OLD: Direct assignment
user.ActiveSubscriptionId = subscription.Id; // Error: Guid → string

// NEW: Convert to string
user.ActiveSubscriptionId = subscription.Id.ToString();
```

### When comparing Guid with string fields:
```csharp
// OLD: Direct comparison
if (plan.Id == user.ActivePlanId) // Error: Guid == string

// NEW: Parse or compare strings
if (plan.Id.ToString() == user.ActivePlanId)
// OR
if (Guid.TryParse(user.ActivePlanId, out var planId) && plan.Id == planId)
```

### When calling LogEventAsync:
```csharp
// OLD: string subscriptionId
await LogEventAsync(user, user.ActiveSubscriptionId, ...)

// NEW: Guid? subscriptionId
await LogEventAsync(user,
    Guid.TryParse(user.ActiveSubscriptionId, out var subId) ? subId : (Guid?)null,
    ...)
```

## Benefits

1. ✅ **Type Safety**: UUID foreign keys prevent invalid references
2. ✅ **Consistency**: All entities use same ID type
3. ✅ **Database Integrity**: Proper foreign key constraints
4. ✅ **Auto-Generation**: Database generates IDs, no client-side string manipulation
5. ✅ **Better Indexing**: UUID columns are more efficient in ClickHouse

## Migration Script

Location: `backend/KSignal.API/Migrations/UpdateSubscriptionsToUseUserId.sql`

Executed successfully on: 2025-12-13

## Status

- ✅ Database tables updated
- ✅ Models updated
- ✅ DbContext configured
- ⚠️  Service and Controller fixes IN PROGRESS (compilation errors remaining)
- ⏳ Testing pending

## Next Steps

1. Fix all compilation errors in Services and Controllers
2. Build and test the application
3. Verify subscription checkout flow works
4. Test webhook processing
5. Verify event logging

