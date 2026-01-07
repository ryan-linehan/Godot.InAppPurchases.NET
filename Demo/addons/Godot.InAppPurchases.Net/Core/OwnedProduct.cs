using System;
using System.Text.Json.Serialization;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Represents a product owned by the user.
/// This is runtime data cached locally, but the platform provider is the source of truth.
/// </summary>
public class OwnedProduct
{
    /// <summary>
    /// The product ID (matches Product.Id).
    /// </summary>
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// When the purchase was made.
    /// </summary>
    [JsonPropertyName("purchasedAt")]
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Platform-specific transaction identifier.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Which provider processed this purchase (Steam, StoreKit, GooglePlay, Local).
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Creates an OwnedProduct from a successful purchase.
    /// </summary>
    public static OwnedProduct FromPurchase(string productId, string transactionId, string provider)
    {
        return new OwnedProduct
        {
            ProductId = productId,
            TransactionId = transactionId,
            Provider = provider,
            PurchasedAt = DateTime.UtcNow
        };
    }
}
