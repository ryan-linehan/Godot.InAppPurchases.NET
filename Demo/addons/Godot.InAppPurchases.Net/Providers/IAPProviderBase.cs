using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers;

/// <summary>
/// Base class for IAP providers with common functionality.
/// </summary>
public abstract partial class IAPProviderBase : RefCounted, IIAPProvider
{
    /// <summary>
    /// Emitted when a purchase completes (success or failure).
    /// </summary>
    [Signal]
    public delegate void PurchaseCompletedEventHandler(string productId, bool success, string error);

    /// <summary>
    /// Emitted when restore completes.
    /// </summary>
    [Signal]
    public delegate void RestoreCompletedEventHandler(bool success, int count, string error);

    /// <summary>
    /// The product catalog for looking up product definitions.
    /// </summary>
    protected ProductCatalog? Catalog { get; private set; }

    /// <inheritdoc/>
    public abstract string ProviderName { get; }

    /// <inheritdoc/>
    public abstract bool IsAvailable { get; }

    /// <inheritdoc/>
    public bool IsInitialized { get; protected set; }

    /// <summary>
    /// Creates a new provider instance.
    /// </summary>
    /// <param name="catalog">The product catalog.</param>
    protected IAPProviderBase(ProductCatalog? catalog)
    {
        Catalog = catalog;
    }

    /// <inheritdoc/>
    public abstract Task<bool> InitializeAsync();

    /// <inheritdoc/>
    public abstract void Purchase(string platformProductId);

    /// <inheritdoc/>
    public abstract Task<PurchaseResult> PurchaseAsync(string platformProductId);

    /// <inheritdoc/>
    public abstract void RestorePurchases();

    /// <inheritdoc/>
    public abstract Task<RestoreResult> RestorePurchasesAsync();

    /// <inheritdoc/>
    public abstract bool IsOwned(string platformProductId);

    /// <inheritdoc/>
    public abstract IEnumerable<string> GetOwnedProductIds();

    /// <inheritdoc/>
    public abstract Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds);

    /// <summary>
    /// Emits the PurchaseCompleted signal safely.
    /// </summary>
    protected void EmitPurchaseCompleted(string productId, bool success, string error = "")
    {
        EmitSignal(SignalName.PurchaseCompleted, productId, success, error ?? string.Empty);
    }

    /// <summary>
    /// Emits the RestoreCompleted signal safely.
    /// </summary>
    protected void EmitRestoreCompleted(bool success, int count, string error = "")
    {
        EmitSignal(SignalName.RestoreCompleted, success, count, error ?? string.Empty);
    }

    /// <summary>
    /// Logs an info message through the IAP logger.
    /// </summary>
    protected void LogInfo(string message)
    {
        IAPLogger.Info($"[{ProviderName}] {message}");
    }

    /// <summary>
    /// Logs a warning message through the IAP logger.
    /// </summary>
    protected void LogWarning(string message)
    {
        IAPLogger.Warning($"[{ProviderName}] {message}");
    }

    /// <summary>
    /// Logs an error message through the IAP logger.
    /// </summary>
    protected void LogError(string message)
    {
        IAPLogger.Error($"[{ProviderName}] {message}");
    }

    /// <summary>
    /// Logs a debug message through the IAP logger.
    /// </summary>
    protected void LogDebug(string message)
    {
        IAPLogger.Debug($"[{ProviderName}] {message}");
    }
}
