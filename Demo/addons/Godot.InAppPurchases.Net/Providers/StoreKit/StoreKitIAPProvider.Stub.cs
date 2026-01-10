#if !GODOT_IOS
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.StoreKit;

/// <summary>
/// Stub implementation for non-iOS platforms.
/// StoreKit is only available on iOS.
/// </summary>
public class StoreKitIAPProvider : IIAPProvider
{
    /// <summary>
    /// StoreKit is only supported on iOS.
    /// </summary>
    public static bool IsPlatformSupported => false;

    /// <inheritdoc/>
    public string ProviderName => ProviderNames.StoreKit;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public bool IsInitialized => false;

    /// <summary>
    /// Creates a new StoreKit IAP provider stub.
    /// </summary>
    public StoreKitIAPProvider(ProductCatalog? catalog)
    {
    }

    /// <inheritdoc/>
    public Task<bool> InitializeAsync()
        => Task.FromResult(false);

    /// <inheritdoc/>
    public void Purchase(string platformProductId) { }

    /// <inheritdoc/>
    public Task<PurchaseResult> PurchaseAsync(string platformProductId)
        => Task.FromResult(PurchaseResult.Failure("StoreKit is not supported on this platform", PurchaseErrorCode.PlatformNotSupported, platformProductId));

    /// <inheritdoc/>
    public void RestorePurchases() { }

    /// <inheritdoc/>
    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failure("StoreKit is not supported on this platform"));

    /// <inheritdoc/>
    public bool IsOwned(string platformProductId) => false;

    /// <inheritdoc/>
    public IEnumerable<string> GetOwnedProductIds() => Enumerable.Empty<string>();

    /// <inheritdoc/>
    public Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
        => Task.FromResult(new Dictionary<string, string>());
}
#endif
