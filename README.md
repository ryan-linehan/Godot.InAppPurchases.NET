# Godot.InAppPurchases.NET

A cross-platform In-App Purchase (IAP) plugin for Godot 4+ with C#/.NET support. Abstracts platform-specific IAP APIs behind a unified interface for iOS (StoreKit) and Android (Google Play Billing).

## Features

- **Unified API** - Single interface for purchases across all platforms
- **Platform Abstraction** - Stub pattern enables cross-platform compilation
- **Non-Consumable Purchases** - One-time purchases with restore support
- **Local Testing** - Debug provider for testing without store accounts
- **Localized Prices** - Fetch regional pricing from iOS and Android stores

## Supported Platforms

| Platform | Provider | Plugin Required |
|----------|----------|-----------------|
| iOS | `StoreKitIAPProvider` | [GodotApplePlugins](https://github.com/migueldeicaza/GodotApplePlugins) |
| Android | `GooglePlayIAPProvider` | [godot-google-play-billing](https://github.com/godot-sdk-integrations/godot-google-play-billing) |
| All | `LocalIAPProvider` | None (built-in) |

## Installation

1. Copy the `addons/Godot.InAppPurchases.Net` folder to your project's `addons/` directory
2. Enable the plugin in **Project > Project Settings > Plugins**
3. Install the platform plugins you need (see table above)
4. Configure providers in **Project Settings > Addons > IAP > Providers**

## Quick Start

### 1. Define Products

Create products in code or as a `.tres` resource file:

```csharp
var catalog = new ProductCatalog();
catalog.Products = new Godot.Collections.Array<Product>
{
    new Product
    {
        Id = "premium_upgrade",
        DisplayName = "Premium Upgrade",
        Description = "Unlock all premium features",
        AppleProductId = "com.game.premium", // App Store product ID
        GoogleProductId = "premium_upgrade"  // Google Play product ID
    }
};
```

### 2. Make Purchases

```csharp
public partial class StoreUI : Control
{
    public override void _Ready()
    {
        // Wait for initialization before using IAPManager
        IAPManager.Instance.InitializationComplete += OnInitializationComplete;
        IAPManager.Instance.PurchaseCompleted += OnPurchaseCompleted;
        IAPManager.Instance.PricesLoaded += OnPricesLoaded;
    }

    private void OnInitializationComplete(bool hasActiveProvider)
    {
        if (hasActiveProvider)
        {
            // Load prices from the store
            IAPManager.Instance.RefreshPrices();
        }
        else
        {
            GD.PrintErr("No IAP provider available");
        }
    }

    private void OnBuyButtonPressed()
    {
        IAPManager.Instance.InitiatePurchase("premium_upgrade");
    }

    private void OnPurchaseCompleted(string productId, bool success, string error)
    {
        if (success)
        {
            GD.Print($"Purchased: {productId}");
            UnlockContent(productId);
        }
        else
        {
            GD.PrintErr($"Purchase failed: {error}");
        }
    }

    private void OnPricesLoaded()
    {
        var price = IAPManager.Instance.GetLocalizedPrice("premium_upgrade");
        buyButton.Text = $"Buy Premium - {price}";
    }
}
```

### 3. Check Ownership

```csharp
// Queries the platform provider directly
if (IAPManager.Instance.IsOwned("premium_upgrade"))
{
    EnablePremiumFeatures();
}
```

### 4. Restore Purchases (Required for iOS)

```csharp
private async void OnRestoreButtonPressed()
{
    var result = await IAPManager.Instance.RestorePurchasesAsync();

    if (result.Success)
    {
        GD.Print($"Restored {result.RestoredCount} purchases");
    }
}
```

## API Reference

### IAPManager (Singleton)

The main interface for all IAP operations. Accessed via `IAPManager.Instance`.

#### Signals

| Signal | Parameters | Description |
|--------|------------|-------------|
| `InitializationComplete` | `hasActiveProvider` | Fired when providers are ready |
| `PurchaseCompleted` | `productId`, `success`, `error` | Fired when purchase completes |
| `PurchaseRestored` | `productId` | Fired for each restored product |
| `RestoreCompleted` | `success`, `restoredCount`, `error` | Fired when restore completes |
| `PricesLoaded` | - | Fired when prices are fetched |
| `ProviderRegistered` | `providerName` | Fired when provider initializes |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `InitiatePurchase(productId)` | `void` | Start purchase (fire-and-forget) |
| `InitiatePurchaseAsync(productId)` | `Task<PurchaseResult>` | Start purchase (async) |
| `RestorePurchases()` | `void` | Restore purchases (fire-and-forget) |
| `RestorePurchasesAsync()` | `Task<RestoreResult>` | Restore purchases (async) |
| `IsOwned(productId)` | `bool` | Check if product is owned (queries provider) |
| `GetOwnedProductIds()` | `IEnumerable<string>` | Get all owned product IDs |
| `GetLocalizedPrice(productId)` | `string` | Get formatted price |
| `RefreshPrices()` | `void` | Fetch prices from store |
| `GetProduct(productId)` | `Product?` | Get product from catalog |
| `GetAllProducts()` | `IEnumerable<Product>` | Get all products |
| `GetActiveProvider()` | `IIAPProvider?` | Get current provider |
| `SetActiveProvider(name)` | `bool` | Switch active provider |

### PurchaseResult

Returned from `InitiatePurchaseAsync()`:

```csharp
public class PurchaseResult
{
    public bool Success { get; set; }
    public string ProductId { get; set; }
    public string TransactionId { get; set; }
    public string Error { get; set; }
    public PurchaseErrorCode ErrorCode { get; set; }

    // For server-side validation
    public string ReceiptData { get; set; }  // iOS: receipt, Android: token
    public string Signature { get; set; }     // Android only
}
```

### RestoreResult

Returned from `RestorePurchasesAsync()`:

```csharp
public class RestoreResult
{
    public bool Success { get; set; }
    public int RestoredCount { get; set; }
    public List<string> RestoredProductIds { get; set; }
    public string Error { get; set; }
}
```

## Project Settings

Configure in **Project Settings > Addons > IAP**:

| Setting | Default | Description |
|---------|---------|-------------|
| `catalog_path` | `res://products.tres` | Path to product catalog |
| `providers/enable_storekit` | `false` | Enable iOS StoreKit |
| `providers/enable_google_play` | `false` | Enable Google Play Billing |
| `providers/enable_local_debug_provider` | `true` | Enable local testing provider |
| `logging/log_level` | `Info` | Logging verbosity |

## Platform Differences

### Purchase Flow

| Platform | Behavior |
|----------|----------|
| **iOS** | Shows native StoreKit purchase dialog |
| **Android** | Shows native Google Play purchase dialog |
| **Local** | Simulates purchase with configurable delay |

### Prices

| Platform | `GetLocalizedPrice()` Returns |
|----------|------------------------------|
| **iOS** | Localized price (e.g., "$4.99", "€4,49") |
| **Android** | Localized price (e.g., "$4.99", "₹399") |
| **Local** | Configurable test price |

### Restore Purchases

| Platform | Behavior |
|----------|----------|
| **iOS** | Required - Apple mandates restore button |
| **Android** | Queries existing purchases |
| **Local** | Returns in-memory purchases |

## Provider Selection

Providers are registered in this order: StoreKit → GooglePlay → Local. The first available provider becomes active.

To test with LocalIAPProvider when a platform provider is available:
```csharp
// Force switch to local provider for testing
IAPManager.Instance.SetActiveProvider("Local");
```

## Security Considerations

This plugin provides **client-side convenience**. For secure implementations:

1. Use `PurchaseResult.ReceiptData` for server-side validation
2. Validate receipts with platform APIs before granting content
3. Don't rely solely on `IsOwned()` for competitive/multiplayer features

| Use Case | Recommendation |
|----------|----------------|
| Cosmetics, single-player content | Client-side OK |
| Remove ads | Client-side usually OK |
| Multiplayer advantages | Server validation required |
| Virtual currency | Server validation required |

## Local Testing

The `LocalIAPProvider` enables testing without store accounts:

```csharp
// Get the local provider
var localProvider = IAPManager.Instance.GetProvider("Local") as LocalIAPProvider;

// Configure test behavior
localProvider.SimulatedPurchaseDelayMs = 1000;  // 1 second delay
localProvider.DefaultTestPrice = "$9.99 (Test)";

// Simulate failures
localProvider.SimulateNextPurchaseFailure = true;
localProvider.SimulatedFailureMessage = "Card declined";

// Manually grant/revoke for testing
localProvider.GrantOwnership("premium_upgrade");
localProvider.RevokeOwnership("premium_upgrade");
localProvider.ClearAllOwnership();
```

**Note:** LocalIAPProvider purchases are stored in memory only. They reset when the app restarts.

## Troubleshooting

### Provider not initializing

1. Check that the required platform plugin is installed
2. Verify the provider is enabled in project settings
3. Check logs for initialization errors (`IAPLogger`)
4. Wait for `InitializationComplete` signal before calling IAP methods

### iOS purchases failing

1. Verify App Store Connect product IDs match `AppleProductId`
2. Ensure StoreKit entitlements are configured
3. Check that `GodotApplePlugins` is properly installed

### Android purchases failing

1. Verify Google Play Console product IDs match `GoogleProductId`
2. Ensure the app is signed with the correct key
3. Check that `godot-google-play-billing` is properly installed
4. Google Play requires acknowledgment within 3 days (handled automatically)

### No active provider

If `InitializationComplete` fires with `hasActiveProvider = false`:
1. No platform providers are enabled, or
2. Platform plugins are missing, or
3. You're on an unsupported platform

The local debug provider is enabled by default - disable it in project settings for production.

## License

MIT License - See LICENSE file for details.

## Credits

- Based on patterns from [Godot.Achievements.NET](https://github.com/ryan-linehan/Godot.Achievements.NET)
- Uses [GodotApplePlugins](https://github.com/migueldeicaza/GodotApplePlugins) for iOS integration
- Uses [godot-google-play-billing](https://github.com/godot-sdk-integrations/godot-google-play-billing) for Android integration
