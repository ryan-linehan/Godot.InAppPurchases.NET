#if !GODOT_PC
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.Steamworks;

/// <summary>
/// Stub implementation for non-PC platforms.
/// Steam is not supported on mobile platforms.
/// </summary>
public class SteamIAPProvider : IIAPProvider
{
    /// <summary>
    /// Steam is only supported on PC platforms.
    /// </summary>
    public static bool IsPlatformSupported => false;

    /// <inheritdoc/>
    public string ProviderName => ProviderNames.Steam;

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public bool IsInitialized => false;

    /// <summary>
    /// Creates a new Steam IAP provider stub.
    /// </summary>
    public SteamIAPProvider(ProductCatalog? catalog)
    {
    }

    /// <inheritdoc/>
    public Task<bool> InitializeAsync()
        => Task.FromResult(false);

    /// <inheritdoc/>
    public void Purchase(string platformProductId) { }

    /// <inheritdoc/>
    public Task<PurchaseResult> PurchaseAsync(string platformProductId)
        => Task.FromResult(PurchaseResult.Failure("Steam is not supported on this platform", PurchaseErrorCode.PlatformNotSupported, platformProductId));

    /// <inheritdoc/>
    public void RestorePurchases() { }

    /// <inheritdoc/>
    public Task<RestoreResult> RestorePurchasesAsync()
        => Task.FromResult(RestoreResult.Failure("Steam is not supported on this platform"));

    /// <inheritdoc/>
    public bool IsOwned(string platformProductId) => false;

    /// <inheritdoc/>
    public IEnumerable<string> GetOwnedProductIds() => Enumerable.Empty<string>();

    /// <inheritdoc/>
    public Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
        => Task.FromResult(new Dictionary<string, string>());
}
#endif
