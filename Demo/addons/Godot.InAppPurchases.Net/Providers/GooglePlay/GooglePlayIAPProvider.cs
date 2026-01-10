#if GODOT_ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.GooglePlay;

/// <summary>
/// Google Play Billing IAP provider.
/// Uses GodotPlayGameServices plugin for Google Play integration.
/// </summary>
/// <remarks>
/// Google Play Billing requirements:
/// - Purchases must be acknowledged within 3 days or they are refunded
/// - Non-consumable purchases should be queried on startup
/// </remarks>
public class GooglePlayIAPProvider : IAPProviderBase
{
    /// <summary>
    /// Google Play is supported on Android platforms.
    /// </summary>
    public static bool IsPlatformSupported => true;

    private readonly ProductCatalog? _catalog;
    private Node? _billingClient;
    private Node? _signInClient;
    private GodotObject? _playGameServices;
    private bool _isInitialized;
    private bool _isConnected;
    private bool _isAuthenticated;

    private TaskCompletionSource<PurchaseResult>? _purchaseTcs;
    private TaskCompletionSource<RestoreResult>? _queryPurchasesTcs;
    private TaskCompletionSource<Dictionary<string, string>>? _pricesTcs;

    private readonly HashSet<string> _ownedProductIds = new();
    private readonly Dictionary<string, string> _cachedPrices = new();
    private readonly Dictionary<string, string> _pendingAcknowledgments = new();

    /// <inheritdoc/>
    public override string ProviderName => ProviderNames.GooglePlay;

    /// <inheritdoc/>
    public override bool IsAvailable => _isInitialized && _isConnected;

    /// <summary>
    /// Creates a new Google Play IAP provider.
    /// </summary>
    /// <param name="catalog">The product catalog containing Google Product IDs.</param>
    public GooglePlayIAPProvider(ProductCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public override async Task<bool> InitializeAsync()
    {
        try
        {
            LogInfo("Initializing Google Play IAP provider...");

            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree == null)
            {
                LogError("Failed to get SceneTree");
                return false;
            }

            // Get GodotPlayGameServices autoload
            _playGameServices = sceneTree.Root.GetNodeOrNull("GodotPlayGameServices");
            if (_playGameServices == null)
            {
                LogWarning("GodotPlayGameServices autoload not found. Make sure the plugin is installed.");
                return false;
            }

            // Initialize the plugin
            _playGameServices.Call("initialize");

            // Create billing client
            await CreateBillingClientAsync(sceneTree);
            if (_billingClient == null)
            {
                LogError("Failed to create billing client");
                return false;
            }

            // Create sign-in client for authentication
            await CreateSignInClientAsync(sceneTree);

            // Connect to billing service
            await ConnectBillingAsync();

            if (_isConnected)
            {
                // Query existing purchases
                await QueryExistingPurchasesAsync();

                // Load product details for catalog items
                var productIds = GetGoogleProductIds();
                if (productIds.Count > 0)
                {
                    await QueryProductDetailsAsync(productIds);
                }
            }

            _isInitialized = true;
            IsInitialized = true;
            LogInfo($"Google Play IAP provider initialized. Connected: {_isConnected}");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Google Play IAP provider: {ex.Message}");
            return false;
        }
    }

    private async Task CreateBillingClientAsync(SceneTree sceneTree)
    {
        try
        {
            // Load the billing client script
            var script = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/billing/billing_client.gd");
            if (script == null)
            {
                LogWarning("Billing client script not found");
                return;
            }

            _billingClient = new Node();
            _billingClient.SetScript(script);
            sceneTree.Root.CallDeferred("add_child", _billingClient);

            // Wait a frame for the node to be added
            await sceneTree.ToSignal(sceneTree, "process_frame");

            ConnectBillingSignals();
        }
        catch (Exception ex)
        {
            LogError($"Failed to create billing client: {ex.Message}");
        }
    }

    private async Task CreateSignInClientAsync(SceneTree sceneTree)
    {
        try
        {
            var script = GD.Load<Script>("res://addons/GodotPlayGameServices/scripts/sign_in/sign_in_client.gd");
            if (script == null)
            {
                LogWarning("Sign-in client script not found");
                return;
            }

            _signInClient = new Node();
            _signInClient.SetScript(script);
            sceneTree.Root.CallDeferred("add_child", _signInClient);

            await sceneTree.ToSignal(sceneTree, "process_frame");

            ConnectSignInSignals();
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to create sign-in client: {ex.Message}");
        }
    }

    private void ConnectBillingSignals()
    {
        if (_billingClient == null) return;

        try
        {
            // Connection signals
            var connectedCallable = Callable.From(OnBillingConnected);
            var disconnectedCallable = Callable.From(OnBillingDisconnected);

            if (!_billingClient.IsConnected("connected", connectedCallable))
                _billingClient.Connect("connected", connectedCallable);
            if (!_billingClient.IsConnected("disconnected", disconnectedCallable))
                _billingClient.Connect("disconnected", disconnectedCallable);

            // Purchase signals
            var purchaseCompletedCallable = Callable.From<Godot.Collections.Dictionary>(OnPurchaseCompleted);
            var purchaseFailedCallable = Callable.From<string>(OnPurchaseFailed);
            var purchaseCancelledCallable = Callable.From(OnPurchaseCancelled);

            if (!_billingClient.IsConnected("purchase_completed", purchaseCompletedCallable))
                _billingClient.Connect("purchase_completed", purchaseCompletedCallable);
            if (!_billingClient.IsConnected("purchase_failed", purchaseFailedCallable))
                _billingClient.Connect("purchase_failed", purchaseFailedCallable);
            if (!_billingClient.IsConnected("purchase_cancelled", purchaseCancelledCallable))
                _billingClient.Connect("purchase_cancelled", purchaseCancelledCallable);

            // Query signals
            var purchasesLoadedCallable = Callable.From<Godot.Collections.Array>(OnPurchasesLoaded);
            var productsLoadedCallable = Callable.From<Godot.Collections.Array>(OnProductsLoaded);

            if (!_billingClient.IsConnected("purchases_loaded", purchasesLoadedCallable))
                _billingClient.Connect("purchases_loaded", purchasesLoadedCallable);
            if (!_billingClient.IsConnected("products_loaded", productsLoadedCallable))
                _billingClient.Connect("products_loaded", productsLoadedCallable);

            // Acknowledgment signal
            var purchaseAcknowledgedCallable = Callable.From<string>(OnPurchaseAcknowledged);
            if (!_billingClient.IsConnected("purchase_acknowledged", purchaseAcknowledgedCallable))
                _billingClient.Connect("purchase_acknowledged", purchaseAcknowledgedCallable);
        }
        catch (Exception ex)
        {
            LogWarning($"Error connecting billing signals: {ex.Message}");
        }
    }

    private void ConnectSignInSignals()
    {
        if (_signInClient == null) return;

        try
        {
            var authCallable = Callable.From<bool>(OnUserAuthenticated);
            if (!_signInClient.IsConnected("user_authenticated", authCallable))
                _signInClient.Connect("user_authenticated", authCallable);
        }
        catch (Exception ex)
        {
            LogWarning($"Error connecting sign-in signals: {ex.Message}");
        }
    }

    private async Task ConnectBillingAsync()
    {
        if (_billingClient == null) return;

        try
        {
            var tcs = new TaskCompletionSource<bool>();

            void OnConnected()
            {
                tcs.TrySetResult(true);
            }

            var callable = Callable.From(OnConnected);
            _billingClient.Connect("connected", callable, (uint)GodotObject.ConnectFlags.OneShot);

            _billingClient.Call("start_connection");

            var connected = await AsyncTimeoutHelper.AwaitWithTimeout(tcs.Task, 10.0, false);
            _isConnected = connected;

            if (connected)
            {
                LogInfo("Connected to Google Play Billing");
            }
            else
            {
                LogWarning("Failed to connect to Google Play Billing (timeout)");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error connecting to billing: {ex.Message}");
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
                "Google Play Billing is not available",
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
            _billingClient?.Call("purchase", platformProductId);

            var result = await AsyncTimeoutHelper.AwaitWithTimeout(
                _purchaseTcs.Task,
                TimeSpan.FromMinutes(5)
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
            return RestoreResult.Failure("Google Play Billing is not available");
        }

        try
        {
            LogInfo("Querying existing purchases...");

            _queryPurchasesTcs = new TaskCompletionSource<RestoreResult>();
            _billingClient?.Call("query_purchases");

            var result = await AsyncTimeoutHelper.AwaitWithTimeout(
                _queryPurchasesTcs.Task,
                TimeSpan.FromSeconds(15)
            );

            return result ?? RestoreResult.Failure("Query timed out");
        }
        catch (Exception ex)
        {
            LogError($"Query failed: {ex.Message}");
            return RestoreResult.Failure($"Query failed: {ex.Message}");
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

        // Return cached prices
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

        if (missingIds.Count > 0)
        {
            await QueryProductDetailsAsync(missingIds);

            foreach (var id in missingIds)
            {
                if (_cachedPrices.TryGetValue(id, out var price))
                {
                    result[id] = price;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Acknowledges a purchase. Must be called within 3 days of purchase.
    /// </summary>
    public void AcknowledgePurchase(string purchaseToken)
    {
        if (_billingClient == null) return;

        try
        {
            _billingClient.Call("acknowledge_purchase", purchaseToken);
        }
        catch (Exception ex)
        {
            LogError($"Failed to acknowledge purchase: {ex.Message}");
        }
    }

    #region Signal Handlers

    private void OnBillingConnected()
    {
        LogInfo("Billing service connected");
        _isConnected = true;
    }

    private void OnBillingDisconnected()
    {
        LogWarning("Billing service disconnected");
        _isConnected = false;
    }

    private void OnUserAuthenticated(bool authenticated)
    {
        _isAuthenticated = authenticated;
        LogInfo($"User authenticated: {authenticated}");
    }

    private void OnPurchaseCompleted(Godot.Collections.Dictionary purchaseData)
    {
        try
        {
            var productId = purchaseData.GetValueOrDefault("product_id", "").AsString();
            var purchaseToken = purchaseData.GetValueOrDefault("purchase_token", "").AsString();
            var orderId = purchaseData.GetValueOrDefault("order_id", "").AsString();
            var signature = purchaseData.GetValueOrDefault("signature", "").AsString();

            LogInfo($"Purchase completed: {productId}");
            _ownedProductIds.Add(productId);

            // Auto-acknowledge the purchase
            if (!string.IsNullOrEmpty(purchaseToken))
            {
                AcknowledgePurchase(purchaseToken);
            }

            _purchaseTcs?.TrySetResult(new PurchaseResult
            {
                Success = true,
                ProductId = productId,
                TransactionId = orderId,
                ReceiptData = purchaseToken,
                Signature = signature
            });
        }
        catch (Exception ex)
        {
            LogError($"Error processing purchase: {ex.Message}");
            _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
                $"Error processing purchase: {ex.Message}",
                PurchaseErrorCode.Unknown,
                ""));
        }
    }

    private void OnPurchaseFailed(string error)
    {
        LogError($"Purchase failed: {error}");
        _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
            error,
            PurchaseErrorCode.PaymentFailed,
            ""));
    }

    private void OnPurchaseCancelled()
    {
        LogInfo("Purchase cancelled by user");
        _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
            "User cancelled",
            PurchaseErrorCode.UserCancelled,
            ""));
    }

    private void OnPurchasesLoaded(Godot.Collections.Array purchases)
    {
        LogInfo($"Purchases loaded: {purchases.Count}");

        var restoredIds = new List<string>();
        _ownedProductIds.Clear();

        foreach (var item in purchases)
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                var purchase = item.AsGodotDictionary();
                var productId = purchase.GetValueOrDefault("product_id", "").AsString();
                var purchaseState = purchase.GetValueOrDefault("purchase_state", 0).AsInt32();

                // Purchase state 1 = purchased
                if (purchaseState == 1 && !string.IsNullOrEmpty(productId))
                {
                    _ownedProductIds.Add(productId);
                    restoredIds.Add(productId);
                }
            }
        }

        _queryPurchasesTcs?.TrySetResult(new RestoreResult
        {
            Success = true,
            RestoredCount = restoredIds.Count,
            RestoredProductIds = restoredIds
        });
    }

    private void OnProductsLoaded(Godot.Collections.Array products)
    {
        LogInfo($"Products loaded: {products.Count}");

        foreach (var item in products)
        {
            if (item.VariantType == Variant.Type.Dictionary)
            {
                var product = item.AsGodotDictionary();
                var productId = product.GetValueOrDefault("product_id", "").AsString();
                var formattedPrice = product.GetValueOrDefault("formatted_price", "").AsString();

                if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(formattedPrice))
                {
                    _cachedPrices[productId] = formattedPrice;
                }
            }
        }

        _pricesTcs?.TrySetResult(_cachedPrices);
    }

    private void OnPurchaseAcknowledged(string productId)
    {
        LogInfo($"Purchase acknowledged: {productId}");
        _pendingAcknowledgments.Remove(productId);
    }

    #endregion

    #region Private Helpers

    private List<string> GetGoogleProductIds()
    {
        if (_catalog == null)
        {
            return new List<string>();
        }

        return _catalog.Products
            .Where(p => !string.IsNullOrEmpty(p.GoogleProductId))
            .Select(p => p.GoogleProductId)
            .ToList();
    }

    private async Task QueryExistingPurchasesAsync()
    {
        if (_billingClient == null) return;

        try
        {
            _queryPurchasesTcs = new TaskCompletionSource<RestoreResult>();
            _billingClient.Call("query_purchases");

            await AsyncTimeoutHelper.AwaitWithTimeout(
                _queryPurchasesTcs.Task,
                TimeSpan.FromSeconds(10)
            );
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to query existing purchases: {ex.Message}");
        }
    }

    private async Task QueryProductDetailsAsync(List<string> productIds)
    {
        if (_billingClient == null || productIds.Count == 0) return;

        try
        {
            LogInfo($"Querying product details for {productIds.Count} products");

            _pricesTcs = new TaskCompletionSource<Dictionary<string, string>>();

            var array = new Godot.Collections.Array();
            foreach (var id in productIds)
            {
                array.Add(id);
            }

            _billingClient.Call("query_product_details", array);

            await AsyncTimeoutHelper.AwaitWithTimeout(
                _pricesTcs.Task,
                TimeSpan.FromSeconds(15)
            );
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to query product details: {ex.Message}");
        }
    }

    #endregion
}
#endif
