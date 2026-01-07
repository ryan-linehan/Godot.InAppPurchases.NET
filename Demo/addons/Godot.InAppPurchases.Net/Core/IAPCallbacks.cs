using System;
using System.Threading.Tasks;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Optional callbacks for custom IAP validation and processing.
/// Set these delegates to hook into the purchase flow.
/// </summary>
public static class IAPCallbacks
{
    /// <summary>
    /// Called before a purchase is acknowledged (Google Play).
    /// Return false to prevent auto-acknowledgment (you must acknowledge manually).
    /// Default: null (auto-acknowledge).
    /// </summary>
    /// <remarks>
    /// Use this when you need to validate purchases with your server before acknowledging.
    /// If you return false, you MUST call AcknowledgePurchaseAsync() manually within 3 days
    /// or Google will refund the purchase.
    /// </remarks>
    public static Func<PurchaseResult, bool>? OnBeforeAcknowledge { get; set; }

    /// <summary>
    /// Called after purchase completes but before PurchaseCompleted signal is emitted.
    /// Use for server-side validation. Return false to treat the purchase as failed.
    /// </summary>
    /// <remarks>
    /// This is the recommended hook for server-side validation.
    /// Example:
    /// <code>
    /// IAPCallbacks.OnValidatePurchase = async (result) => {
    ///     var response = await MyServer.ValidatePurchase(result.ReceiptData);
    ///     return response.IsValid;
    /// };
    /// </code>
    /// </remarks>
    public static Func<PurchaseResult, Task<bool>>? OnValidatePurchase { get; set; }

    /// <summary>
    /// Called when IsOwned() returns a different result than the cache.
    /// Provides opportunity to handle discrepancy (e.g., revoke content, show message).
    /// </summary>
    /// <remarks>
    /// Parameters: productId, cachedOwnership, providerOwnership
    /// Example: User owned product in cache but provider says they don't own it anymore.
    /// </remarks>
    public static Action<string, bool, bool>? OnOwnershipMismatch { get; set; }

    /// <summary>
    /// Called before granting restored purchases.
    /// Return false to skip granting this specific product.
    /// </summary>
    /// <remarks>
    /// Use this to validate restored purchases with your server before granting content.
    /// </remarks>
    public static Func<string, Task<bool>>? OnBeforeRestoreGrant { get; set; }

    /// <summary>
    /// Called when a provider is initialized.
    /// </summary>
    public static Action<string>? OnProviderInitialized { get; set; }

    /// <summary>
    /// Called when a provider fails to initialize.
    /// </summary>
    public static Action<string, string>? OnProviderInitializationFailed { get; set; }

    /// <summary>
    /// Resets all callbacks to null.
    /// Useful for testing or when reinitializing the IAP system.
    /// </summary>
    public static void ClearAll()
    {
        OnBeforeAcknowledge = null;
        OnValidatePurchase = null;
        OnOwnershipMismatch = null;
        OnBeforeRestoreGrant = null;
        OnProviderInitialized = null;
        OnProviderInitializationFailed = null;
    }

    /// <summary>
    /// Invokes OnBeforeAcknowledge if set, otherwise returns true (auto-acknowledge).
    /// </summary>
    internal static bool InvokeOnBeforeAcknowledge(PurchaseResult result)
    {
        return OnBeforeAcknowledge?.Invoke(result) ?? true;
    }

    /// <summary>
    /// Invokes OnValidatePurchase if set, otherwise returns true (valid).
    /// </summary>
    internal static async Task<bool> InvokeOnValidatePurchaseAsync(PurchaseResult result)
    {
        if (OnValidatePurchase == null)
        {
            return true;
        }
        return await OnValidatePurchase(result);
    }

    /// <summary>
    /// Invokes OnOwnershipMismatch if set.
    /// </summary>
    internal static void InvokeOnOwnershipMismatch(string productId, bool cached, bool provider)
    {
        OnOwnershipMismatch?.Invoke(productId, cached, provider);
    }

    /// <summary>
    /// Invokes OnBeforeRestoreGrant if set, otherwise returns true (grant).
    /// </summary>
    internal static async Task<bool> InvokeOnBeforeRestoreGrantAsync(string productId)
    {
        if (OnBeforeRestoreGrant == null)
        {
            return true;
        }
        return await OnBeforeRestoreGrant(productId);
    }

    /// <summary>
    /// Invokes OnProviderInitialized if set.
    /// </summary>
    internal static void InvokeOnProviderInitialized(string providerName)
    {
        OnProviderInitialized?.Invoke(providerName);
    }

    /// <summary>
    /// Invokes OnProviderInitializationFailed if set.
    /// </summary>
    internal static void InvokeOnProviderInitializationFailed(string providerName, string error)
    {
        OnProviderInitializationFailed?.Invoke(providerName, error);
    }
}
