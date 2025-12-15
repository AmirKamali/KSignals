-- Migration: Add IsSandbox column to subscription_plans
-- Description: Flags subscription plans that should only be used in sandbox/testing environments
-- Database: ClickHouse

ALTER TABLE kalshi_signals.subscription_plans
ADD COLUMN IsSandbox UInt8 DEFAULT 0;

-- Verify the column exists
DESCRIBE kalshi_signals.subscription_plans;

-- Optional: View a sample of sandbox flags
SELECT Id, Code, Name, IsSandbox FROM kalshi_signals.subscription_plans LIMIT 10;
