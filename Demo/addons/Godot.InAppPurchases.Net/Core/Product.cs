using Godot;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Represents an in-app purchase product definition.
/// This is the editor-time configuration for a purchasable item.
/// </summary>
[GlobalClass]
public partial class Product : Resource
{
    /// <summary>
    /// Unique identifier for this product within your game.
    /// Used for code references and constants generation.
    /// </summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name shown in the editor and optionally in-game.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this product provides.
    /// </summary>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Icon for this product. Used in editor and optionally in-game.
    /// </summary>
    [Export]
    public Texture2D? Icon { get; set; }

    /// <summary>
    /// Steam DLC App ID. Leave empty if not selling on Steam.
    /// </summary>
    [Export]
    public string SteamDlcAppId { get; set; } = string.Empty;

    /// <summary>
    /// Apple App Store product identifier.
    /// Format: com.company.game.productid
    /// </summary>
    [Export]
    public string AppleProductId { get; set; } = string.Empty;

    /// <summary>
    /// Google Play product identifier.
    /// </summary>
    [Export]
    public string GoogleProductId { get; set; } = string.Empty;

    /// <summary>
    /// Custom properties for game-specific metadata.
    /// Use for categorization, sorting, or other custom data.
    /// </summary>
    [Export]
    public Godot.Collections.Dictionary<string, Variant> CustomProperties { get; set; } = new();

    /// <summary>
    /// Product type. Currently only NonConsumable is supported.
    /// </summary>
    internal ProductType Type { get; set; } = ProductType.NonConsumable;

    /// <summary>
    /// Gets the platform-specific product ID for the given provider.
    /// </summary>
    public string GetPlatformProductId(string providerName)
    {
        return providerName switch
        {
            "Steam" => SteamDlcAppId,
            "StoreKit" => AppleProductId,
            "GooglePlay" => GoogleProductId,
            "Local" => Id,
            _ => Id
        };
    }

    /// <summary>
    /// Checks if this product has a platform ID configured for the given provider.
    /// </summary>
    public bool HasPlatformProductId(string providerName)
    {
        var platformId = GetPlatformProductId(providerName);
        return !string.IsNullOrEmpty(platformId);
    }
}
