# Godot.InAppPurchases.NET - Implementation Plan

## Overview

A cross-platform In-App Purchase (IAP) system for Godot 4+ with C#/.NET support. This plugin abstracts platform-specific IAP APIs behind a unified interface, enabling developers to target Steam (DLC), iOS (StoreKit), and Android (Google Play Billing) from a single codebase.

**Initial Scope:** One-time (non-consumable) purchases with restore functionality.
**Future Considerations:** Consumables and subscriptions (architecture will accommodate these).

---

## Architecture Overview

The plugin is runtime-only (no editor dock). Products are defined in code or loaded from resource files.

```
addons/
└── Godot.InAppPurchases.Net/
    ├── Core/                    # Runtime classes
    ├── Providers/               # Platform-specific implementations
    │   ├── Local/               # Debug/testing provider
    │   ├── Steamworks/          # Steam DLC API
    │   ├── StoreKit/            # iOS App Store
    │   └── GooglePlay/          # Google Play Billing
    ├── IAPPlugin.cs             # Main EditorPlugin entry point
    └── plugin.cfg               # Plugin configuration
```

> **Note:** The Demo folder (with Main.cs, Main.tscn, demo.csproj, etc.) exists only to demonstrate plugin usage and is not part of the plugin itself.

---

## Core Components

### 1. Data Models (`Core/`)

#### `Product.cs`
```csharp
public partial class Product : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public Texture2D? Icon { get; set; }

    // Platform-specific product IDs
    [Export] public string SteamDlcAppId { get; set; }    // Steam DLC App ID
    [Export] public string AppleProductId { get; set; }   // App Store product ID
    [Export] public string GoogleProductId { get; set; }  // Google Play product ID

    // Custom properties (like achievements)
    [Export] public Godot.Collections.Dictionary<string, Variant> CustomProperties { get; set; }

    // Internal: Product type (for future expansion, not exposed in editor yet)
    internal ProductType Type { get; set; } = ProductType.NonConsumable;
}

// Internal enum - not exposed in editor for v1
internal enum ProductType
{
    NonConsumable,   // One-time purchase (current scope)
    Consumable,      // Can be purchased multiple times (future)
    Subscription     // Recurring payments (future)
}
```

#### `ProductCatalog.cs`
```csharp
public partial class ProductCatalog : Resource
{
    [Export] public Godot.Collections.Array<Product> Products { get; set; }

    public Product GetProduct(string id);
    public bool HasProduct(string id);
    public void AddProduct(Product product);
    public void RemoveProduct(string id);
}
```

#### `OwnedProduct.cs`
```csharp
/// <summary>
/// Represents a product owned by the user.
/// This is cached locally but the platform provider is the source of truth.
/// </summary>
public class OwnedProduct
{
    public string ProductId { get; set; }
    public DateTime PurchasedAt { get; set; }
    public string TransactionId { get; set; }
    public string Provider { get; set; }
}
```

### 2. Runtime Manager (`Core/IAPManager.cs`)

The singleton autoload that handles all IAP operations:

```csharp
public partial class IAPManager : Node
{
    // Signals
    [Signal] public delegate void PurchaseCompletedEventHandler(string productId, bool success, string error);
    [Signal] public delegate void PurchaseRestoredEventHandler(string productId);
    [Signal] public delegate void RestoreCompletedEventHandler(bool success, int restoredCount, string error);
    [Signal] public delegate void ProviderRegisteredEventHandler(string providerName);
    [Signal] public delegate void ProviderUnregisteredEventHandler(string providerName);
    [Signal] public delegate void PricesLoadedEventHandler();

    // Core API - Synchronous (fire-and-forget)
    public void InitiatePurchase(string productId);
    public void RestorePurchases();

    // Core API - Async
    public Task<PurchaseResult> InitiatePurchaseAsync(string productId);
    public Task<RestoreResult> RestorePurchasesAsync();

    // Query API - Queries the active provider (platform is source of truth)
    public bool IsOwned(string productId);
    public OwnedProduct GetOwnedProduct(string productId);
    public IEnumerable<OwnedProduct> GetAllOwnedProducts();
    public Product GetProduct(string productId);
    public IEnumerable<Product> GetAllProducts();

    // Price API - All pricing comes from providers (regional pricing)
    public string GetLocalizedPrice(string productId);
    public void RefreshPrices();

    // Provider API
    public IIAPProvider GetProvider(string name);
    public IIAPProvider GetActiveProvider();
    public IEnumerable<IIAPProvider> GetRegisteredProviders();
}
```

### 3. Provider System (`Providers/`)

#### `IIAPProvider.cs` - Interface
```csharp
public interface IIAPProvider
{
    // Static property for compile-time platform check (implemented via stub pattern)
    // static virtual bool IsPlatformSupported { get; }

    string ProviderName { get; }
    bool IsAvailable { get; }      // Runtime: SDK loaded, user logged in, etc.
    bool IsInitialized { get; }    // Has Initialize() completed successfully

    // Lifecycle
    Task<bool> InitializeAsync();

    // Purchase operations
    void Purchase(string platformProductId);  // Fire-and-forget
    Task<PurchaseResult> PurchaseAsync(string platformProductId);

    // Restore (required for non-consumables on iOS)
    void RestorePurchases();
    Task<RestoreResult> RestorePurchasesAsync();

    // Ownership check - provider is source of truth
    bool IsOwned(string platformProductId);
    IEnumerable<string> GetOwnedProductIds();

    // Pricing - must come from provider (regional pricing)
    Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds);
}
```

#### `IAPProviderBase.cs` - Abstract Base
```csharp
public abstract partial class IAPProviderBase : RefCounted, IIAPProvider
{
    // Signals for async operation completion
    [Signal] public delegate void PurchaseCompletedEventHandler(string productId, bool success, string error);
    [Signal] public delegate void RestoreCompletedEventHandler(bool success, int count, string error);

    public abstract string ProviderName { get; }
    public abstract bool IsAvailable { get; }
    public bool IsInitialized { get; protected set; }

    // Common logging, error handling utilities
    protected void LogInfo(string message);
    protected void LogWarning(string message);
    protected void LogError(string message);
}
```

#### Provider Implementations with Stub Pattern

Each provider has two files:
1. **Main implementation** with `#if PLATFORM` directive
2. **Stub implementation** with `#if !PLATFORM` that returns `IsPlatformSupported => false`

| Provider | Platform | Files | Notes |
|----------|----------|-------|-------|
| `LocalIAPProvider` | All | Single file | Debug/testing, simulates purchases |
| `SteamIAPProvider` | PC | `.cs` + `.Stub.cs` | Steam DLC API only |
| `StoreKitIAPProvider` | iOS | `.cs` + `.Stub.cs` | Uses GodotApplePlugins |
| `GooglePlayIAPProvider` | Android | `.cs` + `.Stub.cs` | Uses GodotPlayGameServices |

**Example: Steam Provider Pattern**

```csharp
// SteamIAPProvider.cs
#if GODOT_PC
public class SteamIAPProvider : IAPProviderBase
{
    public static bool IsPlatformSupported => true;

    public override string ProviderName => ProviderNames.Steam;
    public override bool IsAvailable => GodotSteamworks.Instance?.IsSteamRunning ?? false;

    // Full implementation using Steam DLC API...
    public bool IsOwned(string dlcAppId)
    {
        return GodotSteamworks.Instance.IsDlcInstalled(uint.Parse(dlcAppId));
    }
}
#endif

// SteamIAPProvider.Stub.cs
#if !GODOT_PC
public class SteamIAPProvider : IIAPProvider
{
    public static bool IsPlatformSupported => false;

    public string ProviderName => ProviderNames.Steam;
    public bool IsAvailable => false;
    public bool IsInitialized => false;

    // No-op sync methods
    public void Purchase(string productId) { }
    public void RestorePurchases() { }
    public bool IsOwned(string productId) => false;
    public IEnumerable<string> GetOwnedProductIds() => Enumerable.Empty<string>();

    // Failure async methods
    public Task<bool> InitializeAsync()
        => Task.FromResult(false);

    public Task<PurchaseResult> PurchaseAsync(string productId)
        => Task.FromResult(PurchaseResult.Failure("Steam is not supported on this platform"));

    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failure("Steam is not supported on this platform"));

    public Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> productIds)
        => Task.FromResult(new Dictionary<string, string>());
}
#endif
```

### 4. Local Cache (`Core/`)

#### `IAPCache.cs`
```csharp
/// <summary>
/// Local cache for owned products.
/// Used for offline access and debugging, but platform provider is always source of truth.
/// </summary>
public static class IAPCache
{
    // Cache location: user://iap_cache.json
    public static void SaveCache(IEnumerable<OwnedProduct> products, DateTime timestamp);
    public static List<OwnedProduct> LoadCache();
    public static DateTime? GetCacheTimestamp();
    public static bool IsCacheValid();  // Based on settings
    public static void ClearCache();
}
```

**Source of Truth:**
- **Platform provider** is authoritative for ownership
- Local cache is for:
  - Offline mode (show previously owned content)
  - Debug provider testing
  - Faster initial load (then refresh from provider)

**Cache Behavior (controlled by settings):**
- `cache_validity_seconds`: How long cache is considered fresh (0 = always stale)
- `verify_on_launch`: Always verify with provider on app launch
- `on_cache_miss`: What to do when cached ownership doesn't match provider

---

## Preprocessor Directives & Cross-Platform Support

### Provider Initialization

```csharp
// In IAPManager.cs
private void InitializeProviders()
{
    // Always try to register platform providers - stubs handle unsupported platforms
    if (_settings.EnableSteam && SteamIAPProvider.IsPlatformSupported)
    {
        RegisterProvider(new SteamIAPProvider(_catalog));
    }

    if (_settings.EnableStoreKit && StoreKitIAPProvider.IsPlatformSupported)
    {
        RegisterProvider(new StoreKitIAPProvider(_catalog));
    }

    if (_settings.EnableGooglePlay && GooglePlayIAPProvider.IsPlatformSupported)
    {
        RegisterProvider(new GooglePlayIAPProvider(_catalog));
    }

    // Local provider for debugging (optional, controlled by setting)
    if (_settings.EnableLocalDebugProvider)
    {
        RegisterProvider(new LocalIAPProvider(_catalog));
    }
}
```

### Project Settings (Defined in Plugin)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| **Catalog** |
| `iap/catalog_path` | String | `res://products.tres` | Path to product catalog |
| **Providers** |
| `iap/enable_steam` | Bool | `false` | Enable Steam DLC provider |
| `iap/enable_storekit` | Bool | `false` | Enable iOS StoreKit |
| `iap/enable_google_play` | Bool | `false` | Enable Google Play Billing |
| `iap/enable_local_debug_provider` | Bool | `true` | Enable local debug provider |
| **Cache** |
| `iap/cache_validity_seconds` | Int | `3600` | How long cache is valid (0 = always verify) |
| `iap/verify_on_launch` | Bool | `true` | Always verify ownership with provider on launch |
| **Logging** |
| `iap/log_level` | Enum | `Info` | Logging verbosity |

---

## Implementation Phases

### Phase 1: Core Infrastructure ✅
- [x] Project structure and plugin.cfg
- [x] Core data models (Product, ProductCatalog, OwnedProduct)
- [x] IAPManager singleton with basic API
- [x] IIAPProvider interface and base class
- [x] LocalIAPProvider for testing
- [x] Local cache (for debug/offline)
- [x] Project settings registration
- [x] Platform provider stubs (Steam, StoreKit, GooglePlay)

### Phase 2: Platform Providers ✅
- [x] SteamIAPProvider (Steam DLC API) - full implementation
- [x] StoreKitIAPProvider (iOS) - full implementation
- [x] GooglePlayIAPProvider (Android) - full implementation
- [x] Provider initialization based on settings
- [x] AsyncTimeoutHelper utility class

### Phase 3: Demo & Documentation
- [ ] Demo project setup
- [ ] Sample products catalog (.tres file)
- [ ] Demo scene with store UI
- [ ] README documentation

---

## Key Differences from Achievements Plugin

| Aspect | Achievements | IAP |
|--------|-------------|-----|
| Data Type | Progress-based unlocks | Binary ownership |
| Source of Truth | Local (syncs to platforms) | Platform (cached locally) |
| User Interaction | Passive (game triggers) | Active (user initiates purchase) |
| Async Nature | Optional | Required (all purchases are async) |
| Price Data | N/A | Must fetch from provider (regional) |
| Restore Flow | N/A | Required for non-consumables |
| Editor UI | Full dock editor | None (runtime-only) |

---

## Platform Behavior Differences

Different platforms have different capabilities and behaviors. The plugin normalizes these where possible but some differences are inherent:

### Purchase Flow

| Platform | `InitiatePurchase()` Behavior |
|----------|------------------------------|
| **Steam** | Opens Steam overlay/store page to DLC. User purchases through Steam UI. |
| **iOS** | Shows native StoreKit purchase dialog in-app. |
| **Android** | Shows native Google Play purchase dialog in-app. |
| **Local** | Simulates purchase with configurable delay (for testing). |

### Price Fetching

| Platform | `GetLocalizedPrice()` Behavior |
|----------|-------------------------------|
| **Steam** | Returns empty string (prices shown on Steam store page only) |
| **iOS** | Returns localized price from StoreKit (e.g., "$4.99", "€4,49") |
| **Android** | Returns localized price from Play Billing (e.g., "$4.99", "₹399") |
| **Local** | Returns configurable test price |

### Restore Purchases

| Platform | `RestorePurchases()` Behavior |
|----------|------------------------------|
| **Steam** | No-op (DLC ownership is always queryable) |
| **iOS** | Required - restores non-consumables. Apple requires visible restore button. |
| **Android** | Queries existing purchases from Play Store |
| **Local** | Returns cached "purchases" |

### Acknowledgment (Google Play Only)

Google Play requires purchases to be acknowledged within 3 days or they are auto-refunded.

- **Default behavior:** Auto-acknowledge immediately after successful purchase

---

## Security Considerations

### Developer Responsibility

This plugin provides **client-side convenience for managing IAP across platforms**. It is the developer's responsibility to implement appropriate security measures for their use case.

**If purchases are validated entirely on the client:**
- Determined users can and will find ways to bypass client-side checks
- This may be acceptable for cosmetic items or single-player content
- This is NOT acceptable for competitive multiplayer advantages or server-authoritative games

**For secure implementations:**
1. Send receipts from `PurchaseResult.ReceiptData` to your server
2. Your server validates with the platform (Apple/Google/Steam)
3. Your server grants entitlements
4. Client queries your server for ownership, not just local cache

### What This Plugin Provides

| Feature | Security Level | Notes |
|---------|---------------|-------|
| Platform SDK queries | High | Direct query to platform is trustworthy |
| Local cache | Low | File can be modified by users |
| Receipt/token in `PurchaseResult` | Passthrough | Raw data for your server to validate |

### Platform-Specific Validation

| Platform | Validation Data | Server Validation API |
|----------|----------------|----------------------|
| **Steam** | N/A (query DLC directly) | Steam Web API `ISteamUser/CheckAppOwnership` |
| **iOS** | Receipt data | Apple App Store Server API |
| **Android** | Purchase token | Google Play Developer API |

### Recommendations by Use Case

| Use Case | Recommended Approach |
|----------|---------------------|
| Cosmetic items (skins, themes) | Client-side OK |
| Single-player content (levels, characters) | Client-side usually OK |
| Remove ads | Client-side usually OK |
| Competitive multiplayer advantages | Server validation required |
| Virtual currency | Server validation required |
| Subscription features | Server validation required |

---

## API Usage Examples

### Defining Products (Code)
```csharp
// Create products programmatically
var catalog = new ProductCatalog();
catalog.Products = new Godot.Collections.Array<Product>
{
    new Product
    {
        Id = "premium_upgrade",
        DisplayName = "Premium Upgrade",
        Description = "Unlock all premium features",
        SteamDlcAppId = "12345",
        AppleProductId = "com.mygame.premium",
        GoogleProductId = "premium_upgrade"
    },
    new Product
    {
        Id = "character_pack",
        DisplayName = "Character Pack",
        Description = "Additional playable characters",
        SteamDlcAppId = "12346",
        AppleProductId = "com.mygame.characters",
        GoogleProductId = "character_pack"
    }
};
```

### Defining Products (Resource File)
Create a `.tres` file in the Godot editor:
```
[gd_resource type="Resource" script_class="ProductCatalog"]
[ext_resource type="Script" path="res://addons/Godot.InAppPurchases.Net/Core/ProductCatalog.cs" id="1"]

[resource]
script = ExtResource("1")
Products = []
```

### Basic Purchase Flow
```csharp
public partial class StoreUI : Control
{
    public override void _Ready()
    {
        IAPManager.Instance.PurchaseCompleted += OnPurchaseCompleted;
        IAPManager.Instance.PricesLoaded += OnPricesLoaded;

        // Load prices from the active provider
        IAPManager.Instance.RefreshPrices();
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
        // Price comes from provider - handles regional pricing automatically
        var price = IAPManager.Instance.GetLocalizedPrice("premium_upgrade");
        buyButton.Text = $"Buy Premium - {price}";
    }
}
```

### Check Ownership
```csharp
public override void _Ready()
{
    // Queries the active platform provider
    if (IAPManager.Instance.IsOwned("premium_upgrade"))
    {
        EnablePremiumFeatures();
    }
}
```

### Restore Purchases
```csharp
private async void OnRestoreButtonPressed()
{
    var result = await IAPManager.Instance.RestorePurchasesAsync();

    if (result.Success)
    {
        GD.Print($"Restored {result.RestoredCount} purchases");
    }
    else
    {
        GD.PrintErr($"Restore failed: {result.Error}");
    }
}
```

---

## Future Enhancements (Out of Scope for v1)

### Editor UI
- Visual product editor dock
- Import/export (JSON/CSV)
- Constants code generation
- Undo/redo support

### Consumables Support
```csharp
// Future API additions
public void Consume(string productId);
public int GetConsumableBalance(string productId);
```

### Subscriptions Support
```csharp
// Future API additions
public bool IsSubscriptionActive(string productId);
public DateTime? GetSubscriptionExpiryDate(string productId);
public SubscriptionStatus GetSubscriptionStatus(string productId);
```

### Additional Providers
- Epic Games Store
- Xbox Live
- PlayStation Store
- Nintendo eShop
- Amazon Appstore

---

## Technical Notes

### Thread Safety
All provider operations run on background threads. The IAPManager marshals results back to the main thread using `CallDeferred`.

### Error Handling
Each async operation returns a result object:
```csharp
public class PurchaseResult
{
    public bool Success { get; set; }
    public string ProductId { get; set; }
    public string TransactionId { get; set; }
    public string Error { get; set; }
    public PurchaseErrorCode ErrorCode { get; set; }

    // Platform-specific data for server validation
    public string ReceiptData { get; set; }      // iOS: Base64 receipt, Android: Purchase token
    public string Signature { get; set; }         // Android: Signature for verification

    public static PurchaseResult Failure(string error, PurchaseErrorCode code = PurchaseErrorCode.Unknown)
        => new() { Success = false, Error = error, ErrorCode = code };
}

public enum PurchaseErrorCode
{
    None,
    UserCancelled,
    PaymentFailed,
    ProductNotFound,
    AlreadyOwned,
    NetworkError,
    StoreUnavailable,
    PlatformNotSupported,
    Unknown
}
```

### Testing Strategy
The `LocalIAPProvider` enables complete testing without platform SDKs:
- Simulates purchase flow with configurable delays
- Can be configured to fail (for error handling testing)
- Persists "purchases" locally for testing persistence
- Always returns `IsPlatformSupported => true`

---

## File Listing (Complete)

```
addons/Godot.InAppPurchases.Net/
├── Core/
│   ├── Product.cs
│   ├── ProductCatalog.cs
│   ├── ProductType.cs          # Internal enum (future use)
│   ├── OwnedProduct.cs
│   ├── IAPManager.cs
│   ├── IAPSettings.cs
│   ├── IAPCache.cs
│   ├── IAPLogger.cs
│   └── LogLevel.cs
├── Providers/
│   ├── IIAPProvider.cs
│   ├── IAPProviderBase.cs
│   ├── ProviderNames.cs
│   ├── Models/
│   │   ├── PurchaseResult.cs
│   │   ├── RestoreResult.cs
│   │   └── PriceResult.cs
│   ├── Local/
│   │   └── LocalIAPProvider.cs
│   ├── Steamworks/
│   │   ├── SteamIAPProvider.cs       # #if GODOT_PC
│   │   └── SteamIAPProvider.Stub.cs  # #if !GODOT_PC
│   ├── StoreKit/
│   │   ├── StoreKitIAPProvider.cs       # #if GODOT_IOS
│   │   └── StoreKitIAPProvider.Stub.cs  # #if !GODOT_IOS
│   └── GooglePlay/
│       ├── GooglePlayIAPProvider.cs       # #if GODOT_ANDROID
│       └── GooglePlayIAPProvider.Stub.cs  # #if !GODOT_ANDROID
├── IAPPlugin.cs
└── plugin.cfg
```

---

## Success Criteria

1. **Cross-Platform:** Same codebase works on Steam, iOS, and Android via stub pattern
2. **Testability:** Local provider enables complete testing without store accounts
3. **Platform Authority:** Ownership queries go to the active platform provider
4. **Extensibility:** New providers can be added by implementing IIAPProvider + Stub
5. **Flexibility:** Products can be defined in code or resource files
