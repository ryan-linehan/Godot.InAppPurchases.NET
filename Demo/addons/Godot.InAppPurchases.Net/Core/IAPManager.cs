using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.InAppPurchases.Providers;
using Godot.InAppPurchases.Providers.GooglePlay;
using Godot.InAppPurchases.Providers.Local;
using Godot.InAppPurchases.Providers.Models;
using Godot.InAppPurchases.Providers.StoreKit;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Main singleton for managing in-app purchases.
/// Registered as an autoload when the plugin is enabled.
/// </summary>
public partial class IAPManager : Node
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static IAPManager? Instance { get; private set; }

    #region Signals

    /// <summary>
    /// Emitted when a purchase completes (success or failure).
    /// </summary>
    [Signal]
    public delegate void PurchaseCompletedEventHandler(string productId, bool success, string error);

    /// <summary>
    /// Emitted when a purchase is restored.
    /// </summary>
    [Signal]
    public delegate void PurchaseRestoredEventHandler(string productId);

    /// <summary>
    /// Emitted when restore operation completes.
    /// </summary>
    [Signal]
    public delegate void RestoreCompletedEventHandler(bool success, int restoredCount, string error);

    /// <summary>
    /// Emitted when a provider is registered.
    /// </summary>
    [Signal]
    public delegate void ProviderRegisteredEventHandler(string providerName);

    /// <summary>
    /// Emitted when a provider is unregistered.
    /// </summary>
    [Signal]
    public delegate void ProviderUnregisteredEventHandler(string providerName);

    /// <summary>
    /// Emitted when prices are loaded from providers.
    /// </summary>
    [Signal]
    public delegate void PricesLoadedEventHandler();

    /// <summary>
    /// Emitted when initialization is complete and providers are ready.
    /// </summary>
    [Signal]
    public delegate void InitializationCompleteEventHandler(bool hasActiveProvider);

    #endregion

    private ProductCatalog? _catalog;
    private readonly Dictionary<string, IIAPProvider> _providers = new();
    private readonly Dictionary<string, string> _prices = new();
    private IIAPProvider? _activeProvider;

    public override void _EnterTree()
    {
        Instance = this;
        IAPLogger.InitializeFromSettings();
        IAPLogger.Info("IAPManager entering tree");
    }

    public override void _Ready()
    {
        IAPLogger.Info("IAPManager ready, initializing...");
        CallDeferred(MethodName.InitializeDeferred);
    }

    public override void _ExitTree()
    {
        IAPLogger.Info("IAPManager exiting tree");
        Instance = null;
    }

    private void InitializeDeferred()
    {
        LoadCatalog();
        InitializeProviders();
    }

    private void LoadCatalog()
    {
        var catalogPath = IAPSettings.GetCatalogPath();

        if (string.IsNullOrEmpty(catalogPath) || !ResourceLoader.Exists(catalogPath))
        {
            IAPLogger.Warning($"Product catalog not found at: {catalogPath}");
            _catalog = new ProductCatalog();
            return;
        }

        var resource = ResourceLoader.Load<ProductCatalog>(catalogPath);
        if (resource != null)
        {
            _catalog = resource;
            IAPLogger.Info($"Loaded product catalog with {_catalog.Products.Count} products");
        }
        else
        {
            IAPLogger.Warning($"Failed to load product catalog from: {catalogPath}");
            _catalog = new ProductCatalog();
        }
    }

    private async void InitializeProviders()
    {
        IAPLogger.Info("Initializing providers...");

        // Register platform providers based on settings and platform support
        // Priority: Platform-specific providers first, then local debug

        // StoreKit provider (iOS only)
        if (IAPSettings.IsStoreKitEnabled() && StoreKitIAPProvider.IsPlatformSupported)
        {
            IAPLogger.Info("Registering StoreKit provider...");
            var storeKitProvider = new StoreKitIAPProvider(_catalog);
            await RegisterProviderAsync(storeKitProvider);
        }

        // Google Play provider (Android only)
        if (IAPSettings.IsGooglePlayEnabled() && GooglePlayIAPProvider.IsPlatformSupported)
        {
            IAPLogger.Info("Registering Google Play provider...");
            var googlePlayProvider = new GooglePlayIAPProvider(_catalog);
            await RegisterProviderAsync(googlePlayProvider);
        }

        // Register local debug provider if enabled (usually for testing)
        if (IAPSettings.IsLocalDebugProviderEnabled())
        {
            IAPLogger.Info("Registering Local debug provider...");
            var localProvider = new LocalIAPProvider(_catalog);
            await RegisterProviderAsync(localProvider);
        }

        IAPLogger.Info($"Provider initialization complete. {_providers.Count} provider(s) registered.");
        EmitSignal(SignalName.InitializationComplete, _activeProvider != null);
    }

    #region Provider Management

    /// <summary>
    /// Registers a provider and optionally initializes it.
    /// </summary>
    public async Task RegisterProviderAsync(IIAPProvider provider)
    {
        if (_providers.ContainsKey(provider.ProviderName))
        {
            IAPLogger.Warning($"Provider already registered: {provider.ProviderName}");
            return;
        }

        IAPLogger.Info($"Registering provider: {provider.ProviderName}");

        var success = await provider.InitializeAsync();
        if (success)
        {
            _providers[provider.ProviderName] = provider;

            // Set as active provider if it's available and we don't have one
            if (_activeProvider == null && provider.IsAvailable)
            {
                _activeProvider = provider;
                IAPLogger.Info($"Active provider set to: {provider.ProviderName}");
            }

            EmitSignal(SignalName.ProviderRegistered, provider.ProviderName);
            IAPLogger.Info($"Provider registered successfully: {provider.ProviderName}");
        }
        else
        {
            IAPLogger.Warning($"Provider failed to initialize: {provider.ProviderName}");
        }
    }

    /// <summary>
    /// Gets a registered provider by name.
    /// </summary>
    public IIAPProvider? GetProvider(string name)
    {
        return _providers.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets the currently active provider.
    /// </summary>
    public IIAPProvider? GetActiveProvider()
    {
        return _activeProvider;
    }

    /// <summary>
    /// Sets the active provider by name.
    /// </summary>
    public bool SetActiveProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            _activeProvider = provider;
            IAPLogger.Info($"Active provider changed to: {name}");
            return true;
        }
        IAPLogger.Warning($"Cannot set active provider - not found: {name}");
        return false;
    }

    /// <summary>
    /// Gets all registered providers.
    /// </summary>
    public IEnumerable<IIAPProvider> GetRegisteredProviders()
    {
        return _providers.Values;
    }

    #endregion

    #region Purchase API

    /// <summary>
    /// Initiates a purchase for a product. Fire-and-forget version.
    /// Listen to PurchaseCompleted signal for results.
    /// </summary>
    public void InitiatePurchase(string productId)
    {
        _ = InitiatePurchaseAsync(productId);
    }

    /// <summary>
    /// Initiates a purchase for a product and waits for result.
    /// </summary>
    public async Task<PurchaseResult> InitiatePurchaseAsync(string productId)
    {
        IAPLogger.Info($"Initiating purchase: {productId}");

        if (_activeProvider == null)
        {
            var error = "No active provider available";
            IAPLogger.Error(error);
            var result = PurchaseResult.Failure(error, PurchaseErrorCode.StoreUnavailable, productId);
            EmitSignal(SignalName.PurchaseCompleted, productId, false, error);
            return result;
        }

        var product = _catalog?.GetProduct(productId);
        if (product == null)
        {
            var error = $"Product not found in catalog: {productId}";
            IAPLogger.Error(error);
            var result = PurchaseResult.Failure(error, PurchaseErrorCode.ProductNotFound, productId);
            EmitSignal(SignalName.PurchaseCompleted, productId, false, error);
            return result;
        }

        var platformProductId = product.GetPlatformProductId(_activeProvider.ProviderName);
        if (string.IsNullOrEmpty(platformProductId))
        {
            var error = $"No platform product ID configured for {_activeProvider.ProviderName}";
            IAPLogger.Error(error);
            var result = PurchaseResult.Failure(error, PurchaseErrorCode.ProductNotFound, productId);
            EmitSignal(SignalName.PurchaseCompleted, productId, false, error);
            return result;
        }

        var purchaseResult = await _activeProvider.PurchaseAsync(platformProductId);

        if (purchaseResult.Success)
        {
            // Update the product ID to be the game's internal ID (not platform ID)
            purchaseResult.ProductId = productId;
        }

        EmitSignal(SignalName.PurchaseCompleted, productId, purchaseResult.Success, purchaseResult.Error);
        return purchaseResult;
    }

    /// <summary>
    /// Restores previously purchased products. Fire-and-forget version.
    /// </summary>
    public void RestorePurchases()
    {
        _ = RestorePurchasesAsync();
    }

    /// <summary>
    /// Restores previously purchased products and waits for result.
    /// </summary>
    public async Task<RestoreResult> RestorePurchasesAsync()
    {
        IAPLogger.Info("Restoring purchases");

        if (_activeProvider == null)
        {
            var error = "No active provider available";
            IAPLogger.Error(error);
            var result = RestoreResult.Failure(error);
            EmitSignal(SignalName.RestoreCompleted, false, 0, error);
            return result;
        }

        var restoreResult = await _activeProvider.RestorePurchasesAsync();

        if (restoreResult.Success)
        {
            foreach (var platformProductId in restoreResult.RestoredProductIds)
            {
                // Convert platform ID back to product ID
                var product = _catalog?.GetProductByPlatformId(platformProductId, _activeProvider.ProviderName);
                var productId = product?.Id ?? platformProductId;
                EmitSignal(SignalName.PurchaseRestored, productId);
            }
        }

        EmitSignal(SignalName.RestoreCompleted, restoreResult.Success, restoreResult.RestoredCount, restoreResult.Error);
        return restoreResult;
    }

    #endregion

    #region Ownership API

    /// <summary>
    /// Checks if a product is owned by the user.
    /// Queries the active provider directly.
    /// </summary>
    public bool IsOwned(string productId)
    {
        if (_activeProvider == null)
        {
            IAPLogger.Warning("IsOwned called but no active provider available");
            return false;
        }

        var product = _catalog?.GetProduct(productId);
        if (product == null)
        {
            return false;
        }

        var platformProductId = product.GetPlatformProductId(_activeProvider.ProviderName);
        if (string.IsNullOrEmpty(platformProductId))
        {
            return false;
        }

        return _activeProvider.IsOwned(platformProductId);
    }

    /// <summary>
    /// Gets all owned product IDs.
    /// Queries the active provider directly.
    /// </summary>
    public IEnumerable<string> GetOwnedProductIds()
    {
        if (_activeProvider == null || _catalog == null)
        {
            return Enumerable.Empty<string>();
        }

        var ownedPlatformIds = _activeProvider.GetOwnedProductIds().ToHashSet();
        var ownedProductIds = new List<string>();

        foreach (var product in _catalog.Products)
        {
            var platformId = product.GetPlatformProductId(_activeProvider.ProviderName);
            if (!string.IsNullOrEmpty(platformId) && ownedPlatformIds.Contains(platformId))
            {
                ownedProductIds.Add(product.Id);
            }
        }

        return ownedProductIds;
    }

    #endregion

    #region Product API

    /// <summary>
    /// Gets a product from the catalog by ID.
    /// </summary>
    public Product? GetProduct(string productId)
    {
        return _catalog?.GetProduct(productId);
    }

    /// <summary>
    /// Gets all products from the catalog.
    /// </summary>
    public IEnumerable<Product> GetAllProducts()
    {
        return _catalog?.Products ?? Enumerable.Empty<Product>();
    }

    #endregion

    #region Price API

    /// <summary>
    /// Gets the localized price for a product.
    /// Returns empty string if price is not available.
    /// </summary>
    public string GetLocalizedPrice(string productId)
    {
        return _prices.GetValueOrDefault(productId, string.Empty);
    }

    /// <summary>
    /// Refreshes prices from the active provider. Fire-and-forget version.
    /// </summary>
    public void RefreshPrices()
    {
        _ = RefreshPricesAsync();
    }

    /// <summary>
    /// Refreshes prices from the active provider.
    /// </summary>
    public async Task RefreshPricesAsync()
    {
        if (_activeProvider == null || _catalog == null)
        {
            return;
        }

        IAPLogger.Info("Refreshing prices from provider");

        var platformIds = _catalog.Products
            .Select(p => p.GetPlatformProductId(_activeProvider.ProviderName))
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();

        if (platformIds.Count == 0)
        {
            return;
        }

        var prices = await _activeProvider.GetLocalizedPricesAsync(platformIds);

        // Map platform prices back to product IDs
        _prices.Clear();
        foreach (var product in _catalog.Products)
        {
            var platformId = product.GetPlatformProductId(_activeProvider.ProviderName);
            if (!string.IsNullOrEmpty(platformId) && prices.TryGetValue(platformId, out var price))
            {
                _prices[product.Id] = price;
            }
        }

        IAPLogger.Info($"Prices refreshed for {_prices.Count} products");
        EmitSignal(SignalName.PricesLoaded);
    }

    #endregion
}
