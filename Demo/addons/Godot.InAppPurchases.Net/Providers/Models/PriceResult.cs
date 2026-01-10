using System.Collections.Generic;

namespace Godot.InAppPurchases.Providers.Models;

/// <summary>
/// Result of a price fetching operation.
/// </summary>
public class PriceResult
{
    /// <summary>
    /// Whether the price fetch was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Map of product ID to localized price string.
    /// Example: { "premium_upgrade": "$4.99" }
    /// </summary>
    public Dictionary<string, string> Prices { get; set; } = new();

    /// <summary>
    /// Error message if the fetch failed.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful price result.
    /// </summary>
    public static PriceResult Successful(Dictionary<string, string>? prices = null)
    {
        return new PriceResult
        {
            Success = true,
            Prices = prices ?? new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Creates a failed price result.
    /// </summary>
    public static PriceResult Failure(string error)
    {
        return new PriceResult
        {
            Success = false,
            Error = error
        };
    }
}
