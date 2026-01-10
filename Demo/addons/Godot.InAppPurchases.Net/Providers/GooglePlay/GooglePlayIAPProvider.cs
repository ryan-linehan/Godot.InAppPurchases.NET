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
/// Uses godot-google-play-billing plugin for Google Play integration.
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
    private GodotObject? _billingClient;
    private bool _isInitialized;
    private bool _isConnected;

    private TaskCompletionSource<bool>? _connectionTcs;
    private TaskCompletionSource<PurchaseResult>? _purchaseTcs;
    private TaskCompletionSource<RestoreResult>? _queryPurchasesTcs;
    private TaskCompletionSource<Dictionary<string, string>>? _productDetailsTcs;

    private readonly HashSet<string> _ownedProductIds = new();
    private readonly Dictionary<string, string> _cachedPrices = new();

    // Product type constants from BillingClient
    private const string ProductTypeInApp = "inapp";

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
            LogInfo("Initializing Google Play Billing provider...");

            // Check if BillingClient class exists (from godot-google-play-billing plugin)
            if (!ClassDB.ClassExists("BillingClient"))
            {
                LogWarning("BillingClient class not found. Make sure godot-google-play-billing plugin is installed.");
                return false;
            }

            // Create BillingClient instance
            _billingClient = ClassDB.Instantiate("BillingClient").AsGodotObject();
            if (_billingClient == null)
            {
                LogError("Failed to instantiate BillingClient");
                return false;
            }

            // Connect signals
            ConnectSignals();

            // Start connection to Google Play
            _connectionTcs = new TaskCompletionSource<bool>();
            _billingClient.Call("start_connection");

            var connected = await AsyncTimeoutHelper.AwaitWithTimeout(
                _connectionTcs.Task,
                15.0,
                false
            );

            if (!connected)
            {
                LogWarning("Failed to connect to Google Play Billing");
                return false;
            }

            _isConnected = true;

            // Query existing purchases
            await QueryExistingPurchasesAsync();

            // Load product details for catalog items
            var productIds = GetGoogleProductIds();
            if (productIds.Count > 0)
            {
                await QueryProductDetailsAsync(productIds);
            }

            _isInitialized = true;
            IsInitialized = true;
            LogInfo("Google Play Billing provider initialized successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Google Play Billing provider: {ex.Message}");
            return false;
        }
    }

    private void ConnectSignals()
    {
        if (_billingClient == null) return;

        try
        {
            // Connection signals
            var connectedCallable = Callable.From(OnConnected);
            var disconnectedCallable = Callable.From(OnDisconnected);
            var connectErrorCallable = Callable.From<int, string>(OnConnectError);

            _billingClient.Connect("connected", connectedCallable);
            _billingClient.Connect("disconnected", disconnectedCallable);
            _billingClient.Connect("connect_error", connectErrorCallable);

            // Purchase signals
            var purchaseUpdatedCallable = Callable.From<Godot.Collections.Dictionary>(OnPurchaseUpdated);
            _billingClient.Connect("on_purchase_updated", purchaseUpdatedCallable);

            // Query signals
            var queryPurchasesCallable = Callable.From<Godot.Collections.Dictionary>(OnQueryPurchasesResponse);
            var queryProductDetailsCallable = Callable.From<Godot.Collections.Dictionary>(OnQueryProductDetailsResponse);

            _billingClient.Connect("query_purchases_response", queryPurchasesCallable);
            _billingClient.Connect("query_product_details_response", queryProductDetailsCallable);

            // Acknowledgment signal
            var acknowledgeCallable = Callable.From<Godot.Collections.Dictionary>(OnAcknowledgePurchaseResponse);
            _billingClient.Connect("acknowledge_purchase_response", acknowledgeCallable);
        }
        catch (Exception ex)
        {
            LogWarning($"Error connecting billing signals: {ex.Message}");
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
            _billingClient?.Call("query_purchases", ProductTypeInApp);

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
            LogInfo($"Acknowledging purchase: {purchaseToken}");
            _billingClient.Call("acknowledge_purchase", purchaseToken);
        }
        catch (Exception ex)
        {
            LogError($"Failed to acknowledge purchase: {ex.Message}");
        }
    }

    #region Signal Handlers

    private void OnConnected()
    {
        LogInfo("Connected to Google Play Billing");
        _isConnected = true;
        _connectionTcs?.TrySetResult(true);
    }

    private void OnDisconnected()
    {
        LogWarning("Disconnected from Google Play Billing");
        _isConnected = false;
    }

    private void OnConnectError(int responseCode, string debugMessage)
    {
        LogError($"Connection error: {responseCode} - {debugMessage}");
        _isConnected = false;
        _connectionTcs?.TrySetResult(false);
    }

    private void OnPurchaseUpdated(Godot.Collections.Dictionary response)
    {
        try
        {
            var responseCode = response.GetValueOrDefault("response_code", -1).AsInt32();

            if (responseCode == 0) // OK
            {
                var purchases = response.GetValueOrDefault("purchases", new Godot.Collections.Array()).AsGodotArray();

                foreach (var item in purchases)
                {
                    if (item.VariantType == Variant.Type.Dictionary)
                    {
                        var purchase = item.AsGodotDictionary();
                        ProcessPurchase(purchase);
                    }
                }
            }
            else if (responseCode == 1) // User cancelled
            {
                LogInfo("Purchase cancelled by user");
                _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
                    "User cancelled",
                    PurchaseErrorCode.UserCancelled,
                    ""));
            }
            else
            {
                var debugMessage = response.GetValueOrDefault("debug_message", "Unknown error").AsString();
                LogError($"Purchase failed: {responseCode} - {debugMessage}");
                _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
                    debugMessage,
                    MapResponseCode(responseCode),
                    ""));
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing purchase update: {ex.Message}");
            _purchaseTcs?.TrySetResult(PurchaseResult.Failure(
                $"Error: {ex.Message}",
                PurchaseErrorCode.Unknown,
                ""));
        }
    }

    private void ProcessPurchase(Godot.Collections.Dictionary purchase)
    {
        var productId = "";
        var products = purchase.GetValueOrDefault("products", new Godot.Collections.Array()).AsGodotArray();
        if (products.Count > 0)
        {
            productId = products[0].AsString();
        }

        var purchaseToken = purchase.GetValueOrDefault("purchase_token", "").AsString();
        var orderId = purchase.GetValueOrDefault("order_id", "").AsString();
        var purchaseState = purchase.GetValueOrDefault("purchase_state", 0).AsInt32();
        var isAcknowledged = purchase.GetValueOrDefault("is_acknowledged", false).AsBool();
        var signature = purchase.GetValueOrDefault("signature", "").AsString();

        LogInfo($"Purchase processed: {productId}, state: {purchaseState}, acknowledged: {isAcknowledged}");

        // Purchase state 1 = purchased
        if (purchaseState == 1)
        {
            _ownedProductIds.Add(productId);

            // Auto-acknowledge if not already acknowledged
            if (!isAcknowledged && !string.IsNullOrEmpty(purchaseToken))
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
        else if (purchaseState == 2) // Pending
        {
            LogInfo($"Purchase pending: {productId}");
            // Don't complete the TCS - wait for final state
        }
    }

    private void OnQueryPurchasesResponse(Godot.Collections.Dictionary response)
    {
        try
        {
            var responseCode = response.GetValueOrDefault("response_code", -1).AsInt32();

            if (responseCode == 0) // OK
            {
                var purchases = response.GetValueOrDefault("purchases", new Godot.Collections.Array()).AsGodotArray();
                var restoredIds = new List<string>();

                _ownedProductIds.Clear();

                foreach (var item in purchases)
                {
                    if (item.VariantType == Variant.Type.Dictionary)
                    {
                        var purchase = item.AsGodotDictionary();
                        var products = purchase.GetValueOrDefault("products", new Godot.Collections.Array()).AsGodotArray();
                        var purchaseState = purchase.GetValueOrDefault("purchase_state", 0).AsInt32();

                        // Purchase state 1 = purchased
                        if (purchaseState == 1 && products.Count > 0)
                        {
                            var productId = products[0].AsString();
                            _ownedProductIds.Add(productId);
                            restoredIds.Add(productId);
                        }
                    }
                }

                LogInfo($"Query purchases: {restoredIds.Count} owned products");

                _queryPurchasesTcs?.TrySetResult(new RestoreResult
                {
                    Success = true,
                    RestoredCount = restoredIds.Count,
                    RestoredProductIds = restoredIds
                });
            }
            else
            {
                var debugMessage = response.GetValueOrDefault("debug_message", "Unknown error").AsString();
                LogError($"Query purchases failed: {responseCode} - {debugMessage}");
                _queryPurchasesTcs?.TrySetResult(RestoreResult.Failure(debugMessage));
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing query purchases: {ex.Message}");
            _queryPurchasesTcs?.TrySetResult(RestoreResult.Failure($"Error: {ex.Message}"));
        }
    }

    private void OnQueryProductDetailsResponse(Godot.Collections.Dictionary response)
    {
        try
        {
            var responseCode = response.GetValueOrDefault("response_code", -1).AsInt32();

            if (responseCode == 0) // OK
            {
                var productDetails = response.GetValueOrDefault("product_details", new Godot.Collections.Array()).AsGodotArray();

                foreach (var item in productDetails)
                {
                    if (item.VariantType == Variant.Type.Dictionary)
                    {
                        var product = item.AsGodotDictionary();
                        var productId = product.GetValueOrDefault("product_id", "").AsString();

                        // Get price from one_time_purchase_offer_details
                        var offerDetails = product.GetValueOrDefault("one_time_purchase_offer_details", new Godot.Collections.Dictionary()).AsGodotDictionary();
                        var formattedPrice = offerDetails.GetValueOrDefault("formatted_price", "").AsString();

                        if (!string.IsNullOrEmpty(productId) && !string.IsNullOrEmpty(formattedPrice))
                        {
                            _cachedPrices[productId] = formattedPrice;
                        }
                    }
                }

                LogInfo($"Product details loaded: {_cachedPrices.Count} prices cached");
            }
            else
            {
                var debugMessage = response.GetValueOrDefault("debug_message", "Unknown error").AsString();
                LogWarning($"Query product details failed: {responseCode} - {debugMessage}");
            }

            _productDetailsTcs?.TrySetResult(_cachedPrices);
        }
        catch (Exception ex)
        {
            LogError($"Error processing product details: {ex.Message}");
            _productDetailsTcs?.TrySetResult(new Dictionary<string, string>());
        }
    }

    private void OnAcknowledgePurchaseResponse(Godot.Collections.Dictionary response)
    {
        var responseCode = response.GetValueOrDefault("response_code", -1).AsInt32();
        if (responseCode == 0)
        {
            LogInfo("Purchase acknowledged successfully");
        }
        else
        {
            var debugMessage = response.GetValueOrDefault("debug_message", "Unknown error").AsString();
            LogWarning($"Acknowledge failed: {responseCode} - {debugMessage}");
        }
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
            _billingClient.Call("query_purchases", ProductTypeInApp);

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

            _productDetailsTcs = new TaskCompletionSource<Dictionary<string, string>>();

            var array = new Godot.Collections.Array();
            foreach (var id in productIds)
            {
                array.Add(id);
            }

            _billingClient.Call("query_product_details", array, ProductTypeInApp);

            await AsyncTimeoutHelper.AwaitWithTimeout(
                _productDetailsTcs.Task,
                TimeSpan.FromSeconds(15)
            );
        }
        catch (Exception ex)
        {
            LogWarning($"Failed to query product details: {ex.Message}");
        }
    }

    private static PurchaseErrorCode MapResponseCode(int responseCode)
    {
        return responseCode switch
        {
            1 => PurchaseErrorCode.UserCancelled,
            2 => PurchaseErrorCode.NetworkError, // SERVICE_UNAVAILABLE
            3 => PurchaseErrorCode.StoreUnavailable, // BILLING_UNAVAILABLE
            4 => PurchaseErrorCode.ProductNotFound, // ITEM_UNAVAILABLE
            5 => PurchaseErrorCode.Unknown, // DEVELOPER_ERROR
            6 => PurchaseErrorCode.Unknown, // ERROR
            7 => PurchaseErrorCode.AlreadyOwned, // ITEM_ALREADY_OWNED
            8 => PurchaseErrorCode.ProductNotFound, // ITEM_NOT_OWNED
            _ => PurchaseErrorCode.Unknown
        };
    }

    #endregion
}
#endif
