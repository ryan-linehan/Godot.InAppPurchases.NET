#if GODOT_PC
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Godot.InAppPurchases.Core;
using Godot.InAppPurchases.Providers.Models;

namespace Godot.InAppPurchases.Providers.Steamworks;

/// <summary>
/// Steam IAP provider for DLC purchases.
/// Uses Godot.Steamworks.NET plugin for Steam integration.
/// </summary>
/// <remarks>
/// Steam DLC works differently from iOS/Android IAP:
/// - No in-app purchase flow - opens Steam store overlay
/// - DLC ownership is always queryable (no restore needed)
/// - Prices are shown on Steam store page only (not fetchable via API)
/// </remarks>
public class SteamIAPProvider : IAPProviderBase
{
    /// <summary>
    /// Steam is supported on PC platforms.
    /// </summary>
    public static bool IsPlatformSupported => true;

    private readonly ProductCatalog? _catalog;
    private GodotObject? _steamworks;
    private bool _isInitialized;

    /// <inheritdoc/>
    public override string ProviderName => ProviderNames.Steam;

    /// <inheritdoc/>
    public override bool IsAvailable => _isInitialized && IsSteamRunning();

    /// <summary>
    /// Creates a new Steam IAP provider.
    /// </summary>
    /// <param name="catalog">The product catalog containing Steam DLC App IDs.</param>
    public SteamIAPProvider(ProductCatalog? catalog)
    {
        _catalog = catalog;
    }

    /// <inheritdoc/>
    public override Task<bool> InitializeAsync()
    {
        try
        {
            LogInfo("Initializing Steam IAP provider...");

            // Get the GodotSteamworks autoload
            var sceneTree = Engine.GetMainLoop() as SceneTree;
            if (sceneTree == null)
            {
                LogError("Failed to get SceneTree");
                return Task.FromResult(false);
            }

            _steamworks = sceneTree.Root.GetNodeOrNull("GodotSteamworks");
            if (_steamworks == null)
            {
                LogWarning("GodotSteamworks autoload not found. Make sure Godot.Steamworks.NET is installed and enabled.");
                return Task.FromResult(false);
            }

            // Check if Steam is initialized
            var isInitialized = _steamworks.Get("IsInitialized");
            if (isInitialized.VariantType != Variant.Type.Bool || !(bool)isInitialized)
            {
                LogWarning("Steam is not initialized. Is Steam running?");
                return Task.FromResult(false);
            }

            _isInitialized = true;
            IsInitialized = true;
            LogInfo("Steam IAP provider initialized successfully");
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            LogError($"Failed to initialize Steam IAP provider: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public override void Purchase(string platformProductId)
    {
        _ = PurchaseAsync(platformProductId);
    }

    /// <inheritdoc/>
    public override Task<PurchaseResult> PurchaseAsync(string platformProductId)
    {
        if (!IsAvailable)
        {
            return Task.FromResult(PurchaseResult.Failure(
                "Steam is not available",
                PurchaseErrorCode.StoreUnavailable,
                platformProductId));
        }

        if (!uint.TryParse(platformProductId, out var dlcAppId))
        {
            return Task.FromResult(PurchaseResult.Failure(
                $"Invalid DLC App ID: {platformProductId}",
                PurchaseErrorCode.ProductNotFound,
                platformProductId));
        }

        // Check if already owned
        if (IsDlcInstalled(dlcAppId))
        {
            return Task.FromResult(PurchaseResult.Failure(
                "DLC is already owned",
                PurchaseErrorCode.AlreadyOwned,
                platformProductId));
        }

        try
        {
            LogInfo($"Opening Steam store for DLC: {dlcAppId}");

            // Open Steam store overlay to the DLC page
            // User will complete purchase through Steam UI
            _steamworks?.Call("open_store_overlay", dlcAppId);

            // For Steam DLC, we can't know if purchase completed
            // Return success to indicate the store was opened
            // The game should check IsOwned() later to verify purchase
            return Task.FromResult(new PurchaseResult
            {
                Success = true,
                ProductId = platformProductId,
                TransactionId = $"steam_dlc_{dlcAppId}",
                // Note: Steam DLC purchase happens in overlay, we can't track completion
            });
        }
        catch (Exception ex)
        {
            LogError($"Failed to open Steam store: {ex.Message}");
            return Task.FromResult(PurchaseResult.Failure(
                $"Failed to open Steam store: {ex.Message}",
                PurchaseErrorCode.Unknown,
                platformProductId));
        }
    }

    /// <inheritdoc/>
    public override void RestorePurchases()
    {
        // No-op for Steam - DLC ownership is always queryable
        LogInfo("RestorePurchases is not needed for Steam DLC");
    }

    /// <inheritdoc/>
    public override Task<RestoreResult> RestorePurchasesAsync()
    {
        // Steam DLC doesn't need restoration - ownership is always queryable
        // Just return current owned DLCs
        var ownedIds = GetOwnedProductIds().ToList();

        LogInfo($"Steam DLC restore: {ownedIds.Count} DLCs owned");

        return Task.FromResult(new RestoreResult
        {
            Success = true,
            RestoredCount = ownedIds.Count,
            RestoredProductIds = ownedIds
        });
    }

    /// <inheritdoc/>
    public override bool IsOwned(string platformProductId)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (!uint.TryParse(platformProductId, out var dlcAppId))
        {
            LogWarning($"Invalid DLC App ID: {platformProductId}");
            return false;
        }

        return IsDlcInstalled(dlcAppId);
    }

    /// <inheritdoc/>
    public override IEnumerable<string> GetOwnedProductIds()
    {
        if (!IsAvailable || _catalog == null)
        {
            yield break;
        }

        foreach (var product in _catalog.Products)
        {
            if (string.IsNullOrEmpty(product.SteamDlcAppId))
            {
                continue;
            }

            if (uint.TryParse(product.SteamDlcAppId, out var dlcAppId) && IsDlcInstalled(dlcAppId))
            {
                yield return product.SteamDlcAppId;
            }
        }
    }

    /// <inheritdoc/>
    public override Task<Dictionary<string, string>> GetLocalizedPricesAsync(IEnumerable<string> platformProductIds)
    {
        // Steam doesn't provide DLC prices through the API
        // Prices are only shown on the Steam store page
        LogInfo("Steam DLC prices are not available through API - shown on Steam store page only");

        return Task.FromResult(new Dictionary<string, string>());
    }

    #region Private Helpers

    private bool IsSteamRunning()
    {
        if (_steamworks == null)
        {
            return false;
        }

        try
        {
            var isInitialized = _steamworks.Get("IsInitialized");
            return isInitialized.VariantType == Variant.Type.Bool && (bool)isInitialized;
        }
        catch
        {
            return false;
        }
    }

    private bool IsDlcInstalled(uint dlcAppId)
    {
        if (_steamworks == null)
        {
            return false;
        }

        try
        {
            // Call the DLC check method on GodotSteamworks
            var result = _steamworks.Call("is_dlc_installed", dlcAppId);
            return result.VariantType == Variant.Type.Bool && (bool)result;
        }
        catch (Exception ex)
        {
            LogError($"Failed to check DLC ownership: {ex.Message}");
            return false;
        }
    }

    #endregion
}
#endif
