namespace Godot.InAppPurchases.Core;

/// <summary>
/// Logging verbosity levels for the IAP system.
/// </summary>
public enum LogLevel
{
    /// <summary>No logging output.</summary>
    None = 0,

    /// <summary>Only error messages.</summary>
    Error = 1,

    /// <summary>Warnings and errors.</summary>
    Warning = 2,

    /// <summary>Informational messages, warnings, and errors.</summary>
    Info = 3,

    /// <summary>Verbose debug output including all messages.</summary>
    Debug = 4
}
