-- Migration: Add subscription support tables and columns
-- Target: ClickHouse (kalshi_signals database)

ALTER TABLE kalshi_signals.Users 
    ADD COLUMN IF NOT EXISTS `StripeCustomerId` Nullable(String);

ALTER TABLE kalshi_signals.Users 
    ADD COLUMN IF NOT EXISTS `ActiveSubscriptionId` Nullable(String);

ALTER TABLE kalshi_signals.Users 
    ADD COLUMN IF NOT EXISTS `ActivePlanId` Nullable(String);

ALTER TABLE kalshi_signals.Users 
    ADD COLUMN IF NOT EXISTS `SubscriptionStatus` Nullable(String);

CREATE TABLE IF NOT EXISTS kalshi_signals.subscription_plans
(
    `Id` String,
    `Code` String,
    `Name` String,
    `StripePriceId` String,
    `Currency` String,
    `Interval` String,
    `Amount` Decimal(18, 2),
    `IsActive` UInt8,
    `Description` Nullable(String),
    `CreatedAt` DateTime,
    `UpdatedAt` DateTime
) ENGINE = MergeTree()
ORDER BY Id;

CREATE TABLE IF NOT EXISTS kalshi_signals.user_subscriptions
(
    `Id` String,
    `UserId` UInt64,
    `PlanId` String,
    `StripeSubscriptionId` Nullable(String),
    `StripeCustomerId` Nullable(String),
    `Status` String,
    `CancelAtPeriodEnd` UInt8,
    `CurrentPeriodStart` Nullable(DateTime),
    `CurrentPeriodEnd` Nullable(DateTime),
    `CreatedAt` DateTime,
    `UpdatedAt` DateTime
) ENGINE = MergeTree()
ORDER BY (UserId, Id);

CREATE TABLE IF NOT EXISTS kalshi_signals.subscription_events
(
    `Id` String,
    `UserId` UInt64,
    `SubscriptionId` Nullable(String),
    `EventType` String,
    `Notes` Nullable(String),
    `Data` Nullable(String),
    `CreatedAt` DateTime
) ENGINE = MergeTree()
ORDER BY (UserId, CreatedAt);
