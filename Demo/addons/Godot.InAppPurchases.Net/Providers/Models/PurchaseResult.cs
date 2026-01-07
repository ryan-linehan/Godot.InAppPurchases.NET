namespace Godot.InAppPurchases.Providers.Models;

/// <summary>
/// Result of a purchase operation.
/// </summary>
public class PurchaseResult
{
    /// <summary>
    /// Whether the purchase was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The product ID that was purchased.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Platform-specific transaction identifier.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Error message if the purchase failed.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Error code categorizing the failure.
    /// </summary>
    public PurchaseErrorCode ErrorCode { get; set; } = PurchaseErrorCode.None;

    /// <summary>
    /// Platform-specific receipt data for server validation.
    /// iOS: Base64 encoded receipt.
    /// Android: Purchase token.
    /// Steam: Empty (use Steam Web API).
    /// </summary>
    public string ReceiptData { get; set; } = string.Empty;

    /// <summary>
    /// Android signature for purchase verification.
    /// Empty on other platforms.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful purchase result.
    /// </summary>
    public static PurchaseResult Successful(string productId, string transactionId, string receiptData = "", string signature = "")
    {
        return new PurchaseResult
        {
            Success = true,
            ProductId = productId,
            TransactionId = transactionId,
            ReceiptData = receiptData,
            Signature = signature,
            ErrorCode = PurchaseErrorCode.None
        };
    }

    /// <summary>
    /// Creates a failed purchase result.
    /// </summary>
    public static PurchaseResult Failure(string error, PurchaseErrorCode code = PurchaseErrorCode.Unknown, string productId = "")
    {
        return new PurchaseResult
        {
            Success = false,
            ProductId = productId,
            Error = error,
            ErrorCode = code
        };
    }
}

/// <summary>
/// Categorizes purchase failure reasons.
/// </summary>
public enum PurchaseErrorCode
{
    /// <summary>No error.</summary>
    None = 0,

    /// <summary>User cancelled the purchase.</summary>
    UserCancelled,

    /// <summary>Payment could not be processed.</summary>
    PaymentFailed,

    /// <summary>Product ID not found in store.</summary>
    ProductNotFound,

    /// <summary>User already owns this product.</summary>
    AlreadyOwned,

    /// <summary>Network error during purchase.</summary>
    NetworkError,

    /// <summary>Store is unavailable.</summary>
    StoreUnavailable,

    /// <summary>Platform is not supported.</summary>
    PlatformNotSupported,

    /// <summary>Provider is not initialized.</summary>
    NotInitialized,

    /// <summary>Unknown error.</summary>
    Unknown
}
