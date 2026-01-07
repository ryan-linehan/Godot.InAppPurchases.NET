#if !GODOT_ANDROID
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.GooglePlay;

/// <summary>
/// Stub implementation for non-Android platforms.
/// Google Play Billing is only available on Android.
/// </summary>
public class GooglePlayIAPProvider : IIAPProvider
{
    /// <summary>
    /// Google Play is only supported on Android.
    /// </summary>
    public static bool IsPlatformSupported => false;

    /// <inheritdoc/>
    public string ProviderName => ProviderNames.GooglePlay;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public bool IsInitialized => false;

    /// <summary>
    /// Creates a new Google Play IAP provider stub.
    /// </summary>
    public GooglePlayIAPProvider(ProductCatalog? catalog)
    {
    }

    /// <inheritdoc/>
    public Task<bool> InitializeAsync()
        => Task.FromResult(false);

    /// <inheritdoc/>
    public void Purchase(string platformProductId) { }

    /// <inheritdoc/>
    public Task<PurchaseResult> PurchaseAsync(string platformProductId)
        => Task.FromResult(PurchaseResult.Failure("Google Play is not supported on this platform", PurchaseErrorCode.PlatformNotSupported, platformProductId));

    /// <inheritdoc/>
    public void RestorePurchases() { }

    /// <inheritdoc/>
    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failure("Google Play is not supported on this platform"));

    /// <inheritdoc/>
    public bool IsOwned(string platformProductId) => false;

    /// <inheritdoc/>
    public IEnumerable<string> GetOwnedProductIds() => Enumerable.Empty<string>();

    /// <inheritdoc/>
    public Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
        => Task.FromResult(new Dictionary<string, string>());
}
#endif
