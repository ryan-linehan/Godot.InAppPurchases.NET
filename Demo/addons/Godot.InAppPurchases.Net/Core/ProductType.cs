namespace Godot.InAppPurchases.Core;

/// <summary>
/// Type of in-app purchase product.
/// Currently only NonConsumable is supported.
/// </summary>
internal enum ProductType
{
    /// <summary>
    /// One-time purchase that persists forever (e.g., premium upgrade, level pack).
    /// </summary>
    NonConsumable = 0,

    /// <summary>
    /// Can be purchased multiple times (e.g., coins, gems).
    /// Future: Not yet implemented.
    /// </summary>
    Consumable = 1,

    /// <summary>
    /// Recurring payment (e.g., monthly subscription).
    /// Future: Not yet implemented.
    /// </summary>
    Subscription = 2
}
