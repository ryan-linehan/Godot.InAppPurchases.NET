# Godot.InAppPurchases.NET - Implementation Plan

## Overview

A cross-platform In-App Purchase (IAP) system for Godot 4+ with C#/.NET support. This plugin abstracts platform-specific IAP APIs behind a unified interface, enabling developers to target Steam, iOS (StoreKit), and Android (Google Play Billing) from a single codebase.

**Initial Scope:** One-time (non-consumable) purchases with restore functionality.
**Future Considerations:** Consumables and subscriptions (architecture will accommodate these).

---

## Architecture Overview

Following the same patterns as the Achievements plugin:

```
Demo/
├── addons/
│   └── Godot.InAppPurchases.Net/
│       ├── Core/                    # Runtime classes
│       ├── Editor/                  # Editor dock & tools
│       ├── Providers/               # Platform-specific implementations
│       │   ├── Local/               # Local testing/debug provider
│       │   ├── Steamworks/          # Steam DLC/Microtransactions
│       │   ├── StoreKit/            # iOS App Store
│       │   └── GooglePlay/          # Google Play Billing
│       ├── _purchases/              # Generated resource data folder
│       ├── IAPPlugin.cs             # Main EditorPlugin entry point
│       └── plugin.cfg               # Plugin configuration
├── IAPConstants.cs                  # Generated constants file
├── Main.cs                          # Demo implementation
├── Main.tscn                        # Demo scene
├── demo_purchases.tres              # Sample purchase database
├── demo.csproj                      # C# project
├── demo.sln                         # Solution file
└── project.godot                    # Godot project settings
```

---

## Core Components

### 1. Data Models (`Core/`)

#### `Purchase.cs`
```csharp
public partial class Purchase : Resource
{
    [Export] public string Id { get; set; }
    [Export] public string DisplayName { get; set; }
    [Export] public string Description { get; set; }
    [Export] public string IconPath { get; set; }

    // Platform-specific product IDs
    [Export] public string SteamDlcId { get; set; }        // Steam DLC App ID
    [Export] public string AppleProductId { get; set; }    // App Store product ID
    [Export] public string GoogleProductId { get; set; }   // Google Play product ID

    // Pricing (informational - actual prices come from stores)
    [Export] public string DefaultPriceDisplay { get; set; }  // e.g., "$4.99"

    // Purchase type (for future expansion)
    [Export] public PurchaseType Type { get; set; } = PurchaseType.NonConsumable;

    // Custom properties (like achievements)
    [Export] public Godot.Collections.Dictionary<string, Variant> CustomProperties { get; set; }
}

public enum PurchaseType
{
    NonConsumable,   // One-time purchase (current scope)
    Consumable,      // Can be purchased multiple times (future)
    Subscription     // Recurring payments (future)
}
```

#### `PurchaseDatabase.cs`
```csharp
public partial class PurchaseDatabase : Resource
{
    [Export] public Godot.Collections.Array<Purchase> Purchases { get; set; }

    public Purchase GetPurchase(string id);
    public bool HasPurchase(string id);
    public void AddPurchase(Purchase purchase);
    public void RemovePurchase(string id);
    public void MovePurchase(int fromIndex, int toIndex);
}
```

#### `OwnedPurchase.cs`
```csharp
public class OwnedPurchase
{
    public string PurchaseId { get; set; }
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
    [Signal] public delegate void PurchaseCompletedEventHandler(string purchaseId, bool success, string error);
    [Signal] public delegate void PurchaseRestoredEventHandler(string purchaseId);
    [Signal] public delegate void RestoreCompletedEventHandler(bool success, int restoredCount, string error);
    [Signal] public delegate void ProviderRegisteredEventHandler(string providerName);
    [Signal] public delegate void ProviderUnregisteredEventHandler(string providerName);
    [Signal] public delegate void PricesLoadedEventHandler();

    // Core API - Synchronous (fire-and-forget)
    public void InitiatePurchase(string purchaseId);
    public void RestorePurchases();

    // Core API - Async
    public Task<PurchaseResult> InitiatePurchaseAsync(string purchaseId);
    public Task<RestoreResult> RestorePurchasesAsync();

    // Query API
    public bool IsPurchased(string purchaseId);
    public OwnedPurchase GetOwnedPurchase(string purchaseId);
    public IEnumerable<OwnedPurchase> GetAllOwnedPurchases();
    public Purchase GetPurchase(string purchaseId);
    public IEnumerable<Purchase> GetAllPurchases();

    // Price API
    public string GetLocalizedPrice(string purchaseId);
    public void RefreshPrices();

    // Provider API
    public IIAPProvider GetProvider(string name);
    public IEnumerable<IIAPProvider> GetRegisteredProviders();
}
```

### 3. Provider System (`Providers/`)

#### `IIAPProvider.cs` - Interface
```csharp
public interface IIAPProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    bool IsInitialized { get; }

    Task<bool> InitializeAsync();
    Task<PurchaseResult> PurchaseAsync(Purchase purchase);
    Task<RestoreResult> RestorePurchasesAsync();
    Task<PriceResult> GetPricesAsync(IEnumerable<Purchase> purchases);
    bool IsPurchased(string platformProductId);
}
```

#### `IAPProviderBase.cs` - Abstract Base
```csharp
public abstract partial class IAPProviderBase : Node, IIAPProvider
{
    // Common provider functionality
    // Logging, error handling, etc.
}
```

#### Provider Implementations

| Provider | Platform | Notes |
|----------|----------|-------|
| `LocalIAPProvider` | All | Debug/testing, stores purchases locally |
| `SteamIAPProvider` | PC | Uses Steam DLC or Microtransactions API |
| `StoreKitIAPProvider` | iOS | Uses StoreKit via GodotApplePlugins |
| `GooglePlayIAPProvider` | Android | Uses Google Play Billing via GodotPlayGameServices |

### 4. Local Persistence (`Core/`)

#### `IAPPersistence.cs`
```csharp
public static class IAPPersistence
{
    // Local storage for owned purchases (source of truth)
    public static void SaveOwnedPurchases(IEnumerable<OwnedPurchase> purchases);
    public static List<OwnedPurchase> LoadOwnedPurchases();

    // Save location: user://iap_data.json
}
```

---

## Editor Components

### 1. Editor Dock (`Editor/IAPEditorDock.cs` + `.tscn`)

Main editor interface with:
- **Left Panel:** List of all purchases with drag-reorder support
- **Right Panel:** Details editor for selected purchase
- **Toolbar:** Add, Remove, Duplicate, Import, Export, Generate Constants

### 2. Details Panel (`Editor/IAPEditorDetailsPanel.cs` + `.tscn`)

Edit individual purchase properties:
- ID, Display Name, Description
- Icon picker
- Platform-specific product IDs (Steam, Apple, Google)
- Default price display
- Purchase type (Non-consumable selected, others disabled with "Coming Soon")
- Custom properties editor

### 3. CRUD Operations (`Editor/IAPCrudOperations.cs`)

All operations with full **Undo/Redo** support:
```csharp
public class IAPCrudOperations
{
    private EditorUndoRedoManager _undoRedo;

    public void AddPurchase();
    public void RemovePurchase(string id);
    public void DuplicatePurchase(string id);
    public void MovePurchase(int fromIndex, int toIndex);

    // Each operation registers Do/Undo methods with EditorUndoRedoManager
}
```

### 4. Import/Export (`Editor/IAPImportExport.cs`)

Support for:
- **JSON:** Full round-trip with all properties
- **CSV:** Spreadsheet-friendly format for bulk editing

Export format (JSON):
```json
{
  "purchases": [
    {
      "id": "premium_upgrade",
      "displayName": "Premium Upgrade",
      "description": "Unlock all premium features",
      "steamDlcId": "12345",
      "appleProductId": "com.game.premium",
      "googleProductId": "premium_upgrade",
      "defaultPriceDisplay": "$4.99",
      "type": "NonConsumable"
    }
  ]
}
```

### 5. Constants Generator (`Editor/IAPConstantsGenerator.cs`)

Generates type-safe constants:
```csharp
// Auto-generated - Do not modify
public static class IAPConstants
{
    public static class Ids
    {
        /// <summary>Premium Upgrade - Unlock all premium features</summary>
        public const string PremiumUpgrade = "premium_upgrade";

        /// <summary>Character Pack - Additional playable characters</summary>
        public const string CharacterPack = "character_pack";
    }

    public static class Properties
    {
        public const string Category = "category";
        public const string SortOrder = "sort_order";
    }
}
```

### 6. Validator (`Editor/IAPValidator.cs`)

Validates purchase data:
- Unique IDs
- Required fields (ID, DisplayName)
- Platform ID format validation
- Warnings for missing platform IDs

---

## Preprocessor Directives & Cross-Platform Support

### Conditional Compilation Strategy

```csharp
// In IAPManager.cs - Provider initialization
private void InitializeProviders()
{
    // Local provider always active for testing
    RegisterProvider(new LocalIAPProvider());

#if GODOT_PC && HAS_STEAMWORKS
    if (settings.EnableSteam)
    {
        RegisterProvider(new SteamIAPProvider());
    }
#endif

#if GODOT_IOS && HAS_STOREKIT
    if (settings.EnableStoreKit)
    {
        RegisterProvider(new StoreKitIAPProvider());
    }
#endif

#if GODOT_ANDROID && HAS_GOOGLE_PLAY_BILLING
    if (settings.EnableGooglePlay)
    {
        RegisterProvider(new GooglePlayIAPProvider());
    }
#endif
}
```

### Project Settings (Defined in Plugin)

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `iap/database_path` | String | `res://purchases.tres` | Path to purchase database |
| `iap/enable_steam` | Bool | `false` | Enable Steam provider |
| `iap/enable_storekit` | Bool | `false` | Enable iOS StoreKit |
| `iap/enable_google_play` | Bool | `false` | Enable Google Play Billing |
| `iap/local_provider_enabled` | Bool | `true` | Enable local debug provider |
| `iap/log_level` | Enum | `Info` | Logging verbosity |
| `iap/constants_output_path` | String | `res://IAPConstants.cs` | Generated constants path |
| `iap/constants_class_name` | String | `IAPConstants` | Generated class name |
| `iap/constants_namespace` | String | `` | Optional namespace |

---

## Implementation Phases

### Phase 1: Core Infrastructure
- [ ] Project structure and plugin.cfg
- [ ] Core data models (Purchase, PurchaseDatabase, OwnedPurchase)
- [ ] IAPManager singleton with basic API
- [ ] IIAPProvider interface and base class
- [ ] LocalIAPProvider for testing
- [ ] Local persistence (JSON file storage)
- [ ] Project settings registration

### Phase 2: Editor Foundation
- [ ] IAPPlugin.cs (EditorPlugin entry point)
- [ ] Basic editor dock UI (list + details panel)
- [ ] Purchase selection and editing
- [ ] CRUD operations (Add, Remove, Duplicate)
- [ ] Database save/load

### Phase 3: Editor Polish
- [ ] Undo/Redo support for all operations
- [ ] Drag-and-drop reordering
- [ ] Custom properties editor
- [ ] Icon picker
- [ ] Validation and error display
- [ ] Context menu

### Phase 4: Import/Export & Code Generation
- [ ] JSON import/export
- [ ] CSV import/export
- [ ] Constants generator
- [ ] File dialogs integration

### Phase 5: Platform Providers
- [ ] SteamIAPProvider (Steam DLC API)
- [ ] StoreKitIAPProvider (iOS)
- [ ] GooglePlayIAPProvider (Android)
- [ ] Preprocessor directive setup
- [ ] Provider initialization based on settings

### Phase 6: Demo & Documentation
- [ ] Demo project setup
- [ ] Sample purchases database
- [ ] Demo scene with purchase UI
- [ ] README documentation
- [ ] CHANGELOG

---

## Key Differences from Achievements Plugin

| Aspect | Achievements | IAP |
|--------|-------------|-----|
| Data Type | Progress-based unlocks | Binary ownership |
| Platform Sync | Bidirectional | Platform → Local (purchases are authoritative) |
| User Interaction | Passive (game triggers) | Active (user initiates purchase) |
| Async Nature | Optional | Required (all purchases are async) |
| Price Data | N/A | Must fetch from store |
| Restore Flow | N/A | Required for non-consumables |
| Toast Notifications | Built-in | Not included (game handles UI) |

---

## API Usage Examples

### Basic Purchase Flow
```csharp
public partial class StoreUI : Control
{
    public override void _Ready()
    {
        IAPManager.Instance.PurchaseCompleted += OnPurchaseCompleted;
        IAPManager.Instance.PricesLoaded += OnPricesLoaded;

        // Load prices from stores
        IAPManager.Instance.RefreshPrices();
    }

    private void OnBuyButtonPressed()
    {
        IAPManager.Instance.InitiatePurchase(IAPConstants.Ids.PremiumUpgrade);
    }

    private void OnPurchaseCompleted(string purchaseId, bool success, string error)
    {
        if (success)
        {
            GD.Print($"Purchased: {purchaseId}");
            UnlockContent(purchaseId);
        }
        else
        {
            GD.PrintErr($"Purchase failed: {error}");
        }
    }

    private void OnPricesLoaded()
    {
        var price = IAPManager.Instance.GetLocalizedPrice(IAPConstants.Ids.PremiumUpgrade);
        buyButton.Text = $"Buy Premium - {price}";
    }
}
```

### Check Ownership
```csharp
public override void _Ready()
{
    if (IAPManager.Instance.IsPurchased(IAPConstants.Ids.PremiumUpgrade))
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

### Consumables Support
```csharp
// Future API additions
public void ConsumePurchase(string purchaseId);
public int GetConsumableBalance(string purchaseId);
```

### Subscriptions Support
```csharp
// Future API additions
public bool IsSubscriptionActive(string purchaseId);
public DateTime? GetSubscriptionExpiryDate(string purchaseId);
public SubscriptionStatus GetSubscriptionStatus(string purchaseId);
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
    public string PurchaseId { get; set; }
    public string TransactionId { get; set; }
    public string Error { get; set; }
    public PurchaseErrorCode ErrorCode { get; set; }
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
    Unknown
}
```

### Testing Strategy
The `LocalIAPProvider` enables complete testing without platform SDKs:
- Simulates purchase flow with configurable delays
- Can be configured to fail (for error handling testing)
- Persists "purchases" locally for persistence testing

---

## File Listing (Complete)

```
Demo/addons/Godot.InAppPurchases.Net/
├── Core/
│   ├── Purchase.cs
│   ├── PurchaseDatabase.cs
│   ├── PurchaseType.cs
│   ├── OwnedPurchase.cs
│   ├── IAPManager.cs
│   ├── IAPSettings.cs
│   ├── IAPPersistence.cs
│   ├── IAPLogger.cs
│   ├── LogLevel.cs
│   ├── PurchaseResult.cs
│   ├── RestoreResult.cs
│   └── PriceResult.cs
├── Editor/
│   ├── IAPEditorDock.cs
│   ├── IAPEditorDock.tscn
│   ├── IAPEditorDetailsPanel.cs
│   ├── IAPEditorDetailsPanel.tscn
│   ├── IAPCrudOperations.cs
│   ├── IAPImportExport.cs
│   ├── IAPImportExportHandler.cs
│   ├── IAPConstantsGenerator.cs
│   ├── IAPValidator.cs
│   ├── IAPListContextMenu.cs
│   ├── CustomPropertiesEditor.cs
│   ├── CustomPropertiesEditor.tscn
│   ├── VariantPropertyHolder.cs
│   ├── Models/
│   │   └── GenerationResult.cs
│   └── assets/
│       └── (editor icons)
├── Providers/
│   ├── IIAPProvider.cs
│   ├── IAPProviderBase.cs
│   ├── ProviderNames.cs
│   ├── ProviderLogExtensions.cs
│   ├── AsyncTimeoutHelper.cs
│   ├── Local/
│   │   └── LocalIAPProvider.cs
│   ├── Steamworks/
│   │   └── SteamIAPProvider.cs
│   ├── StoreKit/
│   │   └── StoreKitIAPProvider.cs
│   └── GooglePlay/
│       └── GooglePlayIAPProvider.cs
├── IAPPlugin.cs
└── plugin.cfg
```

---

## Success Criteria

1. **Editor Experience:** Developers can add/edit/remove purchases visually without touching code
2. **Type Safety:** Generated constants prevent typos and enable IDE autocomplete
3. **Cross-Platform:** Same codebase works on Steam, iOS, and Android
4. **Testability:** Local provider enables complete testing without store accounts
5. **Reliability:** Owned purchases persist locally and sync with platforms
6. **Extensibility:** New providers can be added by implementing IIAPProvider
