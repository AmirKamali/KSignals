# Payment System - Quick Start Guide

## Overview

Complete payment link flow with user information passing, frontend redirect handling, and webhook processing to show user upgrades.

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| **[PAYMENT_LINK_FLOW.md](./PAYMENT_LINK_FLOW.md)** | Complete payment link flow with code examples |
| **[User_payment_architecture.md](./User_payment_architecture.md)** | Full architecture and database schema |
| **[WEBHOOK_SETUP.md](./WEBHOOK_SETUP.md)** | Detailed webhook configuration guide |
| **[STRIPE_WEBHOOKS_SUMMARY.md](./STRIPE_WEBHOOKS_SUMMARY.md)** | Quick reference for webhooks |

## 🚀 Quick Start

### 1. Configure Stripe Webhooks

Add webhook endpoint in [Stripe Dashboard](https://dashboard.stripe.com/webhooks):

**URL**: `https://your-api-domain.com/api/subscriptions/webhook`

**Events**:
- ✅ `payment_link.payment_succeeded`
- ✅ `checkout.session.completed`
- ✅ `customer.subscription.created`
- ✅ `customer.subscription.updated`
- ✅ `customer.subscription.deleted`
- ✅ `invoice.payment_succeeded`
- ✅ `payment_intent.succeeded`

Copy the **signing secret** (whsec_...) to your config.

### 2. Update Configuration

```json
{
  "Stripe": {
    "SecretKey": "sk_live_...",
    "PublishableKey": "pk_live_...",
    "WebhookSecret": "whsec_...",
    "SuccessUrl": "https://yourapp.com/subscription/success",
    "CoreDataPriceId": "price_...",
    "PremiumPriceId": "price_..."
  }
}
```

### 3. Frontend Integration

#### Create Checkout
```javascript
// When user clicks "Subscribe" button
const response = await fetch('/api/subscriptions/checkout', {
  method: 'POST',
  headers: {
    'Authorization': `Bearer ${jwtToken}`,
    'Content-Type': 'application/json'
  },
  body: JSON.stringify({
    planCode: 'core-data',
    successUrl: `${window.location.origin}/subscription/success`
  })
});

const { url } = await response.json();
window.location.href = url; // Redirect to Stripe
```

#### Success Page - Check Status
```javascript
// On success page - poll for subscription activation
async function checkSubscription() {
  const maxAttempts = 20;
  const interval = 1000;

  for (let i = 0; i < maxAttempts; i++) {
    const response = await fetch('/api/subscriptions/status', {
      headers: { 'Authorization': `Bearer ${jwtToken}` }
    });

    const data = await response.json();

    if (data.hasActiveSubscription) {
      // Subscription activated!
      console.log('User upgraded to:', data.plan.name);
      redirectToDashboard();
      return;
    }

    await new Promise(resolve => setTimeout(resolve, interval));
  }

  // Timeout - show message
  console.log('Activation taking longer than expected');
}
```

## 🔄 Complete Flow

```
1. User clicks "Subscribe"
   ↓
2. Frontend: POST /api/subscriptions/checkout
   ↓
3. Backend: Creates payment link with user info
   - Pre-creates Stripe customer
   - Includes metadata (userId, firebaseId, email, username)
   - Returns payment link URL
   ↓
4. User: Redirected to Stripe payment page
   ↓
5. User: Completes payment
   ↓
6. Stripe: Redirects to successUrl
   ↓
7. Frontend: Shows "Processing..." + polls /api/subscriptions/status
   ↓
8. Stripe: Sends webhooks to backend (parallel with step 7)
   - payment_link.payment_succeeded
   - customer.subscription.created
   - invoice.payment_succeeded
   ↓
9. Backend: Processes webhooks
   - Resolves user via metadata
   - Updates User.SubscriptionStatus = "active"
   - Creates/updates UserSubscription
   ↓
10. Frontend: Status check returns hasActiveSubscription: true
    ↓
11. Frontend: Shows success + redirects to dashboard
    ↓
12. User: Has premium access ✅
```

## 📝 API Endpoints

### POST /api/subscriptions/checkout
Create payment link

**Request**:
```json
{
  "planCode": "core-data",
  "successUrl": "https://app.com/success" // optional
}
```

**Response**:
```json
{
  "sessionId": "plink_...",
  "paymentLinkId": "plink_...",
  "publicKey": "pk_live_...",
  "url": "https://checkout.stripe.com/c/pay/..."
}
```

### GET /api/subscriptions/status
Check if user has upgraded (use after payment redirect)

**Response**:
```json
{
  "hasActiveSubscription": true,
  "isUpgraded": true,
  "plan": {
    "id": "core-data",
    "name": "Core Data",
    "amount": 79.00,
    "currency": "usd"
  },
  "status": "active",
  "currentPeriodEnd": "2025-02-08T12:00:00Z"
}
```

### GET /api/subscriptions/me
Get full subscription details with event history

**Response**:
```json
{
  "activePlan": { ... },
  "status": "active",
  "currentPeriodEnd": "2025-02-08T12:00:00Z",
  "events": [
    {
      "eventType": "customer.subscription.created",
      "notes": "Subscription activated",
      "createdAt": "2025-01-08T12:00:00Z"
    }
  ]
}
```

### POST /api/subscriptions/portal
Create billing portal session (for managing subscription)

**Response**:
```json
{
  "url": "https://billing.stripe.com/session/..."
}
```

## 🔍 User Information Passed to Stripe

When creating a payment link, the following user information is sent to Stripe:

### Customer Object
```
Email: user@example.com
Name: johndoe
Description: "User ID: 12345, Firebase ID: abc123..."
```

### Metadata (attached to customer, subscription, invoice, payment)
```
firebaseId: "abc123..."
userId: "12345"
userEmail: "user@example.com"
username: "johndoe"
planCode: "core-data"
planId: "core-data"
```

This ensures the user can be associated with the payment through multiple methods:
1. Stripe Customer ID
2. Metadata userId
3. Metadata firebaseId

## 🧪 Testing

### Local Testing
```bash
# 1. Start Stripe CLI
stripe listen --forward-to localhost:5000/api/subscriptions/webhook

# 2. Start your backend
cd backend/KSignal.API
dotnet run

# 3. Test payment link
curl -X POST http://localhost:5000/api/subscriptions/checkout \
  -H "Authorization: Bearer YOUR_JWT" \
  -H "Content-Type: application/json" \
  -d '{"planCode": "core-data"}'

# 4. Use test card: 4242 4242 4242 4242

# 5. Check status
curl http://localhost:5000/api/subscriptions/status \
  -H "Authorization: Bearer YOUR_JWT"
```

### Test Cards
- **Success**: `4242 4242 4242 4242`
- **Decline**: `4000 0000 0000 0002`
- **Requires Auth**: `4000 0025 0000 3155`

## 🛡️ Security

- ✅ **Webhook signature verification**: All webhooks verified with Stripe signature
- ✅ **JWT authentication**: All user endpoints require valid JWT
- ✅ **User isolation**: Users only access their own data
- ✅ **No card storage**: All payment data on Stripe (PCI compliant)
- ✅ **Metadata protection**: Only visible to you, not public

## 📊 Monitoring

### Verify Webhook Delivery
1. Go to [Stripe Dashboard](https://dashboard.stripe.com/webhooks)
2. Click on your webhook endpoint
3. View "Recent deliveries"
4. Check for 200 responses

### Check Database
```sql
-- User subscription status
SELECT SubscriptionStatus, ActivePlanId
FROM Users
WHERE FirebaseId = 'abc123...';

-- Subscription events
SELECT EventType, Notes, CreatedAt
FROM SubscriptionEvents
WHERE UserId = 12345
ORDER BY CreatedAt DESC
LIMIT 10;

-- Active subscription
SELECT Status, CurrentPeriodEnd
FROM UserSubscriptions
WHERE UserId = 12345;
```

## ⚠️ Troubleshooting

### Subscription not activating after payment

**Check**:
1. ✅ Webhooks delivered (Stripe Dashboard)
2. ✅ Webhook signature valid (check logs)
3. ✅ User exists with correct FirebaseId
4. ✅ Metadata includes userId/firebaseId
5. ✅ Events logged in SubscriptionEventLog

### Status check returns hasActiveSubscription: false

**Wait**: Webhooks may take a few seconds to process (status poll waits 20 seconds)

**Check**:
- User.SubscriptionStatus should be "active"
- UserSubscription.CurrentPeriodEnd should be in future
- SubscriptionEventLog should have webhook events

### Payment succeeded but user not found

**Check**:
- Customer metadata has correct firebaseId
- User exists in database with matching FirebaseId
- Check logs for "No user found" warnings

## 🎯 Key Features

✅ **Automatic customer creation** - Creates Stripe customer with user info
✅ **Rich metadata** - All user details passed to Stripe
✅ **Multi-tier user resolution** - 3 fallback methods to find user
✅ **Success redirect** - Customizable redirect after payment
✅ **Status polling endpoint** - Frontend can check upgrade status
✅ **Event logging** - Complete audit trail of all events
✅ **Idempotent webhooks** - Safe to process multiple times
✅ **Clover API support** - Latest Stripe API (2025-11-17)

## 📖 Code Locations

- **Controller**: `backend/KSignal.API/Controllers/SubscriptionsController.cs`
- **Service**: `backend/KSignal.API/Services/StripeSubscriptionService.cs`
- **Models**: `backend/KSignal.API/Models/`
- **Database**: `backend/KSignal.API/Data/KalshiDbContext.cs`

## 🆘 Support

- **Stripe Issues**: [Stripe Support](https://support.stripe.com)
- **Webhook Docs**: [Stripe Webhooks](https://stripe.com/docs/webhooks)
- **Test Cards**: [Stripe Testing](https://stripe.com/docs/testing)
- **API Docs**: [Stripe API](https://stripe.com/docs/api)

## ✅ Production Checklist

Before going live:

- [ ] Stripe webhook endpoint configured (production mode)
- [ ] All 7 webhook events selected
- [ ] Webhook signing secret in configuration
- [ ] Success URL points to production domain
- [ ] Test payment in test mode works end-to-end
- [ ] Subscription status check works on success page
- [ ] User sees "upgraded" status after payment
- [ ] Webhook monitoring alerts set up
- [ ] Database backups configured
- [ ] Support process for payment issues documented

---

**Need more details?** See the full documentation files listed at the top of this document.
