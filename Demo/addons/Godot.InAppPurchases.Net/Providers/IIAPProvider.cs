using System.Collections.Generic;
using System.Threading.Tasks;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers;

/// <summary>
/// Interface for platform-specific IAP implementations.
/// Each platform provider implements this interface.
/// Use the static IsPlatformSupported property (on concrete classes) to check compile-time support.
/// </summary>
public interface IIAPProvider
{
    /// <summary>
    /// Name of this provider (e.g., "Steam", "StoreKit", "GooglePlay").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Runtime availability check.
    /// Returns true if the SDK is loaded, user is logged in, etc.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Whether Initialize() has completed successfully.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the provider.
    /// </summary>
    /// <returns>True if initialization succeeded.</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Initiates a purchase flow. Fire-and-forget version.
    /// Use PurchaseCompleted signal for results.
    /// </summary>
    /// <param name="platformProductId">The platform-specific product ID.</param>
    void Purchase(string platformProductId);

    /// <summary>
    /// Initiates a purchase flow and waits for result.
    /// </summary>
    /// <param name="platformProductId">The platform-specific product ID.</param>
    Task<PurchaseResult> PurchaseAsync(string platformProductId);

    /// <summary>
    /// Restores previously purchased products. Fire-and-forget version.
    /// Required for iOS non-consumables.
    /// </summary>
    void RestorePurchases();

    /// <summary>
    /// Restores previously purchased products and waits for result.
    /// </summary>
    Task<RestoreResult> RestorePurchasesAsync();

    /// <summary>
    /// Checks if a product is owned by the user.
    /// Provider is the source of truth for ownership.
    /// </summary>
    /// <param name="platformProductId">The platform-specific product ID.</param>
    bool IsOwned(string platformProductId);

    /// <summary>
    /// Gets all owned product IDs from this provider.
    /// </summary>
    IEnumerable<string> GetOwnedProductIds();

    /// <summary>
    /// Fetches localized prices for products.
    /// Steam returns empty strings (prices shown on store page only).
    /// </summary>
    /// <param name="platformProductIds">Platform-specific product IDs to fetch prices for.</param>
    Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds);
}
