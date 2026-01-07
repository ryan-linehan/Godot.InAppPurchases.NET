using System.Collections.Generic;

namespace Godot.InAppPurchases.Providers.Models;

/// <summary>
/// Result of a restore purchases operation.
/// </summary>
public class RestoreResult
{
    /// <summary>
    /// Whether the restore operation completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of purchases that were restored.
    /// </summary>
    public int RestoredCount { get; set; }

    /// <summary>
    /// Product IDs that were restored.
    /// </summary>
    public List<string> RestoredProductIds { get; set; } = new();

    /// <summary>
    /// Error message if the restore failed.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful restore result.
    /// </summary>
    public static RestoreResult Successful(List<string>? restoredIds = null)
    {
        var ids = restoredIds ?? new List<string>();
        return new RestoreResult
        {
            Success = true,
            RestoredCount = ids.Count,
            RestoredProductIds = ids
        };
    }

    /// <summary>
    /// Creates a failed restore result.
    /// </summary>
    public static RestoreResult Failure(string error)
    {
        return new RestoreResult
        {
            Success = false,
            Error = error
        };
    }
}
