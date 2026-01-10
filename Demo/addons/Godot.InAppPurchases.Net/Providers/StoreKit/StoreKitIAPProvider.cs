#if GODOT_IOS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.StoreKit;

/// <summary>
/// iOS StoreKit IAP provider.
/// Uses GodotApplePlugins for StoreKit integration.
/// </summary>
/// <remarks>
/// iOS StoreKit requirements:
/// - App must have a visible "Restore Purchases" button
/// - Non-consumable purchases must be restorable
/// - Receipt validation is recommended for secure implementations
/// </remarks>
public class StoreKitIAPProvider : IAPProviderBase
{
    /// <summary>
    /// StoreKit is supported on iOS platforms.
    /// </summary>
    public static bool IsPlatformSupported => true;

    private readonly ProductCatalog? _catalog;
    private GodotObject? _storeKitManager;
    private bool _isInitialized;

    private TaskCompletionSource<PurchaseResult>? _purchaseTcs;
    private TaskCompletionSource<RestoreResult>? _restoreTcs;
    private TaskCompletionSource<Dictionary<string, string>>? _pricesTcs;

    private readonly HashSet<string> _ownedProductIds = new();
    private readonly Dictionary<string, string> _cachedPrices = new();

    /// <inheritdoc/>
    public override string ProviderName => ProviderNames.StoreKit;

    /// <inheritdoc/>
    public override bool IsAvailable => _isInitialized && _storeKitManager != null;

    /// <summary>
    /// Creates a new StoreKit IAP provider.
    /// </summary>
    /// <param name="catalog">The product catalog containing Apple Product IDs.</param>
    public StoreKitIAPProvider(ProductCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public override async Task<bool> InitializeAsync()
    {
        try
        {
            LogInfo("Initializing StoreKit IAP provider...");

            // Check if StoreKit classes exist (from GodotApplePlugins)
            if (!ClassDB.ClassExists("StoreKitManager"))
            {
                LogWarning("StoreKitManager class not found. Make sure GodotApplePlugins is installed.");
                return false;
            }

            // Create StoreKit manager instance
            _storeKitManager = ClassDB.Instantiate("StoreKitManager").AsGodotObject();
            if (_storeKitManager == null)
            {
                LogError("Failed to instantiate StoreKitManager");
                return false;
            }

            // Connect signals
            ConnectSignals();

            // Initialize with product IDs from catalog
            var productIds = GetAppleProductIds();
            if (productIds.Count > 0)
            {
                await RequestProductInfoAsync(productIds);
            }

            _isInitialized = true;
            IsInitialized = true;
            LogInfo("StoreKit IAP provider initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize StoreKit IAP provider: {ex.Message}");
            return false;
        }
    }

    private void ConnectSignals()
    {
        if (_storeKitManager == null) return;

        try
        {
            // Purchase signals
            var purchaseSuccessCallable = Callable.From<string, string>(OnPurchaseSuccess);
            var purchaseFailedCallable = Callable.From<string, string>(OnPurchaseFailed);
            var purchaseCancelledCallable = Callable.From<string>(OnPurchaseCancelled);

            if (!_storeKitManager.IsConnected("purchase_success", purchaseSuccessCallable))
                _storeKitManager.Connect("purchase_success", purchaseSuccessCallable);
            if (!_storeKitManager.IsConnected("purchase_failed", purchaseFailedCallable))
                _storeKitManager.Connect("purchase_failed", purchaseFailedCallable);
            if (!_storeKitManager.IsConnected("purchase_cancelled", purchaseCancelledCallable))
                _storeKitManager.Connect("purchase_cancelled", purchaseCancelledCallable);

            // Restore signals
            var restoreCompletedCallable = Callable.From<Godot.Collections.Array>(OnRestoreCompleted);
            var restoreFailedCallable = Callable.From<string>(OnRestoreFailed);

            if (!_storeKitManager.IsConnected("restore_completed", restoreCompletedCallable))
                _storeKitManager.Connect("restore_completed", restoreCompletedCallable);
            if (!_storeKitManager.IsConnected("restore_failed", restoreFailedCallable))
                _storeKitManager.Connect("restore_failed", restoreFailedCallable);

            // Product info signals
            var productsReceivedCallable = Callable.From<Godot.Collections.Array>(OnProductsReceived);
            var productsFailedCallable = Callable.From<string>(OnProductsFailed);

            if (!_storeKitManager.IsConnected("products_received", productsReceivedCallable))
                _storeKitManager.Connect("products_received", productsReceivedCallable);
            if (!_storeKitManager.IsConnected("products_failed", productsFailedCallable))
                _storeKitManager.Connect("products_failed", productsFailedCallable);
        }
        catch (Exception ex)
        {
            LogWarning($"Error connecting StoreKit signals: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public override void Purchase(string platformProductId)
    {
        _ = PurchaseAsync(platformProductId);
    }

    /// <inheritdoc/>
    public override async Task<PurchaseResult> PurchaseAsync(string platformProductId)
    {
        if (!IsAvailable)
        {
            return PurchaseResult.Failure(
                "StoreKit is not available",
                PurchaseErrorCode.StoreUnavailable,
                platformProductId);
        }

        if (_ownedProductIds.Contains(platformProductId))
        {
            return PurchaseResult.Failure(
                "Product is already owned",
                PurchaseErrorCode.AlreadyOwned,
                platformProductId);
        }

        try
        {
            LogInfo($"Starting purchase for: {platformProductId}");

            _purchaseTcs = new TaskCompletionSource<PurchaseResult>();
            _storeKitManager?.Call("purchase", platformProductId);

            // Wait for purchase result with timeout
            var result = await AsyncTimeoutHelper.AwaitWithTimeout(
                _purchaseTcs.Task,
                TimeSpan.FromMinutes(5) // Purchase can take time for user input
            );

            return result ?? PurchaseResult.Failure(
                "Purchase timed out",
                PurchaseErrorCode.Unknown,
                platformProductId);
        }
        catch (Exception ex)
        {
            LogError($"Purchase failed: {ex.Message}");
            return PurchaseResult.Failure(
                $"Purchase failed: {ex.Message}",
                PurchaseErrorCode.Unknown,
                platformProductId);
        }
    }

    /// <inheritdoc/>
    public override void RestorePurchases()
    {
        _ = RestorePurchasesAsync();
    }

    /// <inheritdoc/>
    public override async Task<RestoreResult> RestorePurchasesAsync()
    {
        if (!IsAvailable)
        {
            return RestoreResult.Failure("StoreKit is not available");
        }

        try
        {
            LogInfo("Restoring purchases...");

            _restoreTcs = new TaskCompletionSource<RestoreResult>();
            _storeKitManager?.Call("restore_purchases");

            // Wait for restore result with timeout
            var result = await AsyncTimeoutHelper.AwaitWithTimeout(
                _restoreTcs.Task,
                TimeSpan.FromSeconds(30)
            );

            return result ?? RestoreResult.Failure("Restore timed out");
        }
        catch (Exception ex)
        {
            LogError($"Restore failed: {ex.Message}");
            return RestoreResult.Failure($"Restore failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public override bool IsOwned(string platformProductId)
    {
        return _ownedProductIds.Contains(platformProductId);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetOwnedProductIds()
    {
        return _ownedProductIds;
    }

    /// <inheritdoc/>
    public override async Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
    {
        if (!IsAvailable)
        {
            return new Dictionary<string, string>();
        }

        var ids = platformProductIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        // Return cached prices if available
        var result = new Dictionary<string, string>();
        var missingIds = new List<string>();

        foreach (var id in ids)
        {
            if (_cachedPrices.TryGetValue(id, out var price))
            {
                result[id] = price;
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count == 0)
        {
            return result;
        }

        // Request missing product info
        await RequestProductInfoAsync(missingIds);

        // Add newly cached prices
        foreach (var id in missingIds)
        {
            if (_cachedPrices.TryGetValue(id, out var price))
            {
                result[id] = price;
            }
        }

        return result;
    }

    #region Signal Handlers

    private void OnPurchaseSuccess(string productId, string transactionId)
    {
        LogInfo($"Purchase successful: {productId}");
        _ownedProductIds.Add(productId);

        // Get receipt data for validation
        var receiptData = GetReceiptData();

        _purchaseTcs?.TrySetResult(new PurchaseResult
        {
            Success = true,
            ProductId = productId,
            TransactionId = transactionId,
            ReceiptData = receiptData
        });
    }

    private void OnPurchaseFailed(string productId, string error)
    {
        LogError($"Purchase failed: {productId} - {error}");
        _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
            error,
            PurchaseErrorCode.PaymentFailed,
            productId));
    }

    private void OnPurchaseCancelled(string productId)
    {
        LogInfo($"Purchase cancelled: {productId}");
        _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
            "User cancelled",
            PurchaseErrorCode.UserCancelled,
            productId));
    }

    private void OnRestoreCompleted(Godot.Collections.Array restoredProducts)
    {
        LogInfo($"Restore completed: {restoredProducts.Count} products");

        var restoredIds = new List<string>();
        foreach (var item in restoredProducts)
        {
            if (item.VariantType == Variant.Type.String)
            {
                var productId = item.AsString();
                _ownedProductIds.Add(productId);
                restoredIds.Add(productId);
            }
        }

        _restoreTcs?.TrySetResult(new RestoreResult
        {
            Success = true,
            RestoredCount = restoredIds.Count,
            RestoredProductIds = restoredIds
        });
    }

    private void OnRestoreFailed(string error)
    {
        LogError($"Restore failed: {error}");
        _restoreTcs?.TrySetResult(RestoreResult.Failure(error));
    }

    private void OnProductsReceived(Godot.Collections.Array products)
    {
        LogInfo($"Products received: {products.Count}");

        foreach (var item in products)
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                var product = item.AsGodotDictionary();
                var productId = product.GetValueOrDefault("product_id", "").AsString();
                var localizedPrice = product.GetValueOrDefault("localized_price", "").AsString();

                if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(localizedPrice))
                {
                    _cachedPrices[productId] = localizedPrice;
                }
            }
        }

        _pricesTcs?.TrySetResult(_cachedPrices);
    }

    private void OnProductsFailed(string error)
    {
        LogWarning($"Failed to load products: {error}");
        _pricesTcs?.TrySetResult(new Dictionary<string, string>());
    }

    #endregion

    #region Private Helpers

    private List<string> GetAppleProductIds()
    {
        if (_catalog == null)
        {
            return new List<string>();
        }

        return _catalog.Products
            .Where(p => !string.IsNullOrEmpty(p.AppleProductId))
            .Select(p => p.AppleProductId)
            .ToList();
    }

    private async Task RequestProductInfoAsync(List<string> productIds)
    {
        if (_storeKitManager == null || productIds.Count == 0)
        {
            return;
        }

        try
        {
            LogInfo($"Requesting product info for {productIds.Count} products");

            _pricesTcs = new TaskCompletionSource<Dictionary<string, string>>();

            var array = new Godot.Collections.Array();
            foreach (var id in productIds)
            {
                array.Add(id);
            }

            _storeKitManager.Call("request_products", array);

            await AsyncTimeoutHelper.AwaitWithTimeout(
                _pricesTcs.Task,
                TimeSpan.FromSeconds(15)
            );
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to request product info: {ex.Message}");
        }
    }

    private string GetReceiptData()
    {
        try
        {
            var result = _storeKitManager?.Call("get_receipt_data");
            if (result?.VariantType == Variant.Type.String)
            {
                return result.Value.AsString();
            }
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to get receipt data: {ex.Message}");
        }

        return string.Empty;
    }

    #endregion
}
#endif
