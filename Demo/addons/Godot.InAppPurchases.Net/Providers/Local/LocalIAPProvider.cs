using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.Local;

/// <summary>
/// Local IAP provider for debugging and testing.
/// Simulates purchases locally without connecting to any store.
/// Purchases are stored in memory only - they reset when the app restarts.
/// Always returns IsPlatformSupported = true.
/// </summary>
public class LocalIAPProvider : IAPProviderBase
{
    /// <summary>
    /// Always supported - used for local testing.
    /// </summary>
    public static bool IsPlatformSupported => true;

    /// <inheritdoc/>
    public override string ProviderName => ProviderNames.Local;

    /// <inheritdoc/>
    public override bool IsAvailable => true;

    /// <summary>
    /// Simulated purchase delay in milliseconds.
    /// Set to 0 for instant purchases.
    /// </summary>
    public int SimulatedPurchaseDelayMs { get; set; } = 500;

    /// <summary>
    /// Simulated restore delay in milliseconds.
    /// </summary>
    public int SimulatedRestoreDelayMs { get; set; } = 300;

    /// <summary>
    /// If true, the next purchase will fail (for testing error handling).
    /// Resets to false after a failed purchase.
    /// </summary>
    public bool SimulateNextPurchaseFailure { get; set; }

    /// <summary>
    /// Error message to use when SimulateNextPurchaseFailure is true.
    /// </summary>
    public string SimulatedFailureMessage { get; set; } = "Simulated purchase failure";

    /// <summary>
    /// Error code to use when SimulateNextPurchaseFailure is true.
    /// </summary>
    public PurchaseErrorCode SimulatedFailureCode { get; set; } = PurchaseErrorCode.Unknown;

    /// <summary>
    /// Default price string to return for products.
    /// </summary>
    public string DefaultTestPrice { get; set; } = "$0.99 (Test)";

    // In-memory storage for owned products (resets on app restart)
    private readonly HashSet<string> _ownedProducts = new();

    /// <summary>
    /// Creates a new local IAP provider.
    /// </summary>
    public LocalIAPProvider(ProductCatalog? catalog) : base(catalog)
    {
    }

    /// <inheritdoc/>
    public override Task<bool> InitializeAsync()
    {
        LogInfo("Initializing local provider");
        IsInitialized = true;
        LogInfo("Local provider initialized");
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public override async void Purchase(string platformProductId)
    {
        var result = await PurchaseAsync(platformProductId);
        EmitPurchaseCompleted(result.ProductId, result.Success, result.Error);
    }

    /// <inheritdoc/>
    public override async Task<PurchaseResult> PurchaseAsync(string platformProductId)
    {
        LogInfo($"Starting purchase for: {platformProductId}");

        if (!IsInitialized)
        {
            return PurchaseResult.Failure("Provider not initialized", PurchaseErrorCode.NotInitialized, platformProductId);
        }

        // Simulate purchase delay
        if (SimulatedPurchaseDelayMs > 0)
        {
            await Task.Delay(SimulatedPurchaseDelayMs);
        }

        // Check for simulated failure
        if (SimulateNextPurchaseFailure)
        {
            SimulateNextPurchaseFailure = false;
            LogInfo($"Simulating purchase failure: {SimulatedFailureMessage}");
            return PurchaseResult.Failure(SimulatedFailureMessage, SimulatedFailureCode, platformProductId);
        }

        // Check if already owned
        if (_ownedProducts.Contains(platformProductId))
        {
            LogInfo($"Product already owned: {platformProductId}");
            return PurchaseResult.Failure("Product already owned", PurchaseErrorCode.AlreadyOwned, platformProductId);
        }

        // Simulate successful purchase
        var transactionId = $"local_{Guid.NewGuid():N}";
        _ownedProducts.Add(platformProductId);

        LogInfo($"Purchase successful: {platformProductId} (Transaction: {transactionId})");

        return PurchaseResult.Successful(
            platformProductId,
            transactionId,
            receiptData: $"local_receipt_{platformProductId}",
            signature: ""
        );
    }

    /// <inheritdoc/>
    public override async void RestorePurchases()
    {
        var result = await RestorePurchasesAsync();
        EmitRestoreCompleted(result.Success, result.RestoredCount, result.Error);
    }

    /// <inheritdoc/>
    public override async Task<RestoreResult> RestorePurchasesAsync()
    {
        LogInfo("Restoring purchases");

        if (!IsInitialized)
        {
            return RestoreResult.Failure("Provider not initialized");
        }

        // Simulate restore delay
        if (SimulatedRestoreDelayMs > 0)
        {
            await Task.Delay(SimulatedRestoreDelayMs);
        }

        // Return currently owned products
        var restoredIds = _ownedProducts.ToList();
        LogInfo($"Restored {restoredIds.Count} purchases");

        return RestoreResult.Successful(restoredIds);
    }

    /// <inheritdoc/>
    public override bool IsOwned(string platformProductId)
    {
        return _ownedProducts.Contains(platformProductId);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetOwnedProductIds()
    {
        return _ownedProducts.ToList();
    }

    /// <inheritdoc/>
    public override Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
    {
        var prices = new Dictionary<string, string>();
        foreach (var productId in platformProductIds)
        {
            prices[productId] = DefaultTestPrice;
        }
        return Task.FromResult(prices);
    }

    /// <summary>
    /// Manually grants ownership of a product (for testing).
    /// </summary>
    public void GrantOwnership(string productId)
    {
        if (_ownedProducts.Add(productId))
        {
            LogInfo($"Granted ownership: {productId}");
        }
    }

    /// <summary>
    /// Manually revokes ownership of a product (for testing).
    /// </summary>
    public void RevokeOwnership(string productId)
    {
        if (_ownedProducts.Remove(productId))
        {
            LogInfo($"Revoked ownership: {productId}");
        }
    }

    /// <summary>
    /// Clears all owned products (for testing).
    /// </summary>
    public void ClearAllOwnership()
    {
        _ownedProducts.Clear();
        LogInfo("Cleared all ownership");
    }
}
