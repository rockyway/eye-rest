# 001 - LemonSqueezy Donation Setup Guide

## Overview

This guide covers setting up LemonSqueezy as the payment and license key provider for Eye-Rest's voluntary donation workflow. LemonSqueezy handles payment processing, tax collection (as Merchant of Record), and automatic license key generation/delivery.

---

## 1. Create a LemonSqueezy Account

1. Go to [lemonsqueezy.com](https://www.lemonsqueezy.com/) and sign up
2. Complete your store setup (name, currency, payout details)
3. Note your **store slug** (e.g., `eyerest`) — this forms part of your checkout URL

---

## 2. Create the Donation Product

### 2.1 Product Setup

1. Go to **Store > Products > New Product**
2. Fill in:
   - **Name**: `Eye-Rest Donation` (or `Support Eye-Rest Development`)
   - **Description**: "Thank you for supporting Eye-Rest! Your donation helps fund continued development and improvements."
   - **Pricing type**: **Pay What You Want**
   - **Minimum price**: Set based on your preference (see Section 3 below)
   - **Suggested price**: `$5.00` (or whatever you'd like to suggest)
3. Upload a product image (use the Eye-Rest app icon)

### 2.2 Enable License Keys

1. In the product editor, go to the **License keys** section
2. Toggle **Generate license keys** to ON
3. Configure:
   - **License key length**: `16` (default is fine)
   - **Activation limit**: `3` (allows user to validate on up to 3 machines)
   - **License length**: **Never expires** (perpetual — a donation doesn't expire)
4. Save the product

### 2.3 Get the Checkout URL

After creating the product, get the checkout URL:
- Go to **Store > Products**, click your donation product
- Click **Share** and copy the checkout URL
- Format: `https://{store-slug}.lemonsqueezy.com/buy/{product-variant-id}`
- Example: `https://eyerest.lemonsqueezy.com/buy/abc123`

Update the `DonationUrl` in `EyeRest.Abstractions/Models/DonationSettings.cs`:

```csharp
public string DonationUrl { get; set; } = "https://eyerest.lemonsqueezy.com/buy/abc123";
```

Or set it in the user's `config.json` under `Donation.DonationUrl`.

---

## 3. Free License Keys for the Owner (You)

**Yes, there are multiple ways to get a valid license key for yourself without paying:**

### Option A: Pay What You Want with $0 Minimum (Recommended)

1. Set the product's **Pricing type** to **Pay What You Want**
2. Set the **Minimum price** to **$0.00**
3. Visit your own checkout URL
4. Enter `$0` as the amount and complete the "purchase"
5. You'll receive a license key in your email receipt
6. Enter this key in the Eye-Rest app to activate donor status

This is the simplest approach. Anyone (including you) can get a free key, and generous users can pay whatever they want.

### Option B: Test Mode Keys

1. In the LemonSqueezy dashboard, toggle **Test Mode** (top-right switch)
2. Create a test product with license keys enabled
3. Make a test purchase — no real payment required
4. Use the generated test license key

**Important**: Test mode keys validate against the test API. For production use, you'll need to decide if the app should support test mode validation or use Option A/C instead.

### Option C: Create a Separate Free Variant

1. Edit your donation product
2. Add a new **Variant** (e.g., "Supporter - Free")
3. Set the variant price to **$0.00**
4. Enable license key generation for this variant
5. Keep this variant **unlisted** (don't show on the public page)
6. Use the direct variant checkout URL for yourself or for special giveaways

### Option D: Generate Keys via API

Use the LemonSqueezy API to create license keys programmatically:

```bash
curl -X POST https://api.lemonsqueezy.com/v1/license-keys \
  -H "Authorization: Bearer {YOUR_API_KEY}" \
  -H "Content-Type: application/vnd.api+json" \
  -H "Accept: application/vnd.api+json" \
  -d '{
    "data": {
      "type": "license-keys",
      "attributes": {
        "order_id": {ORDER_ID},
        "product_id": {PRODUCT_ID},
        "key": "CUSTOM-KEY-IF-DESIRED"
      }
    }
  }'
```

This requires an API key from **Settings > API** in the dashboard.

### Recommendation

**Option A ($0 minimum PWYW)** is the best approach because:
- Simple — no API integration needed for key generation
- You get a real production license key
- Users who want free keys can get them (good for OSS community goodwill)
- Generous users voluntarily donate more
- All keys validate the same way via the License API

---

## 4. How Validation Works

When a user enters a license key in Eye-Rest, the app calls:

```
POST https://api.lemonsqueezy.com/v1/licenses/validate
Content-Type: application/json

{
  "license_key": "XXXX-XXXX-XXXX-XXXX",
  "instance_name": "EyeRest"
}
```

**No API key or authentication is required** for this endpoint — it's a public validation endpoint.

### Response (valid key):
```json
{
  "valid": true,
  "error": null,
  "license_key": {
    "id": 123,
    "status": "active",
    "key": "XXXX-XXXX-XXXX-XXXX",
    "activation_limit": 3,
    "activation_usage": 1,
    ...
  },
  "instance": { ... },
  "meta": { ... }
}
```

### Response (invalid key):
```json
{
  "valid": false,
  "error": "The license key was not found.",
  "license_key": null,
  "instance": null,
  "meta": { ... }
}
```

The app stores the donor state securely (Windows DPAPI / macOS Keychain) so validation only needs to happen once.

---

## 5. Dashboard Management

### Viewing License Keys
- Go to **Store > Licenses** to see all generated keys
- Filter by status (active, inactive, expired, disabled)

### Revoking a Key
- Click the key in the dashboard
- Click **Disable** to revoke it
- The key will fail future validation attempts

### Regenerating a Key
- If a customer loses their key, you can regenerate it from the dashboard
- The old key is disabled and a new one is issued

---

## 6. Fees

| Scenario | Fee |
|----------|-----|
| Platform fee | **$0/mo** (free to use) |
| Per transaction | **5% + $0.50** |
| $0 transactions | **No fee** (nothing to charge) |
| Payouts | Free (ACH/Wire) |

LemonSqueezy acts as **Merchant of Record**, meaning they handle:
- Sales tax / VAT collection and remittance
- Payment processing (Stripe under the hood)
- Receipt emails with license keys
- Refunds

---

## 7. Configuration Checklist

- [ ] LemonSqueezy account created and verified
- [ ] Donation product created with **Pay What You Want** pricing
- [ ] License key generation enabled on the product
- [ ] Checkout URL copied and updated in `DonationSettings.DonationUrl`
- [ ] Test purchase completed to verify license key delivery
- [ ] License key validated in Eye-Rest app successfully
- [ ] (Optional) Generated a $0 key for your own use

---

## 8. Code Reference

| File | Purpose |
|------|---------|
| `EyeRest.Abstractions/Models/DonationSettings.cs` | `DonationUrl` and tracking fields |
| `EyeRest.Abstractions/Services/IDonationService.cs` | Service interface |
| `EyeRest.Core/Services/DonationService.cs` | LemonSqueezy API validation logic |
| `EyeRest.UI/Views/DonationCodeDialog.axaml` | License key input dialog |
| `EyeRest.UI/Views/DonationBannerView.axaml` | Dismissible donation banner |
| `EyeRest.UI/Views/AboutWindow.axaml` | Donate + Enter Code buttons |

---

## References

- [LemonSqueezy Licensing Overview](https://docs.lemonsqueezy.com/help/licensing)
- [Generating License Keys](https://docs.lemonsqueezy.com/help/licensing/generating-license-keys)
- [Validating License Keys (Tutorial)](https://docs.lemonsqueezy.com/guides/tutorials/license-keys)
- [License API - Validate](https://docs.lemonsqueezy.com/api/license-api/validate-license-key)
- [Pay What You Want](https://docs.lemonsqueezy.com/help/products/pay-what-you-want)
- [Getting Started with the API](https://docs.lemonsqueezy.com/guides/developer-guide/getting-started)
- [LemonSqueezy Pricing](https://www.lemonsqueezy.com/pricing)
