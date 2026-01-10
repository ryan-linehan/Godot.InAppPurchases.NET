using Godot;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Centralized logging for the IAP system.
/// Respects the log level setting from project settings.
/// </summary>
public static class IAPLogger
{
    private const string LogPrefix = "[IAP] ";

    /// <summary>
    /// Current log level. Messages below this level are suppressed.
    /// </summary>
    public static LogLevel CurrentLevel { get; set; } = LogLevel.Info;

    /// <summary>
    /// Logs a debug message (most verbose).
    /// </summary>
    public static void Debug(string message)
    {
        if (CurrentLevel >= LogLevel.Debug)
        {
            GD.Print($"{LogPrefix}[DEBUG] {message}");
        }
    }

    /// <summary>
    /// Logs an info message.
    /// </summary>
    public static void Info(string message)
    {
        if (CurrentLevel >= LogLevel.Info)
        {
            GD.Print($"{LogPrefix}[INFO] {message}");
        }
    }

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public static void Warning(string message)
    {
        if (CurrentLevel >= LogLevel.Warning)
        {
            GD.PushWarning($"{LogPrefix}{message}");
        }
    }

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public static void Error(string message)
    {
        if (CurrentLevel >= LogLevel.Error)
        {
            GD.PushError($"{LogPrefix}{message}");
        }
    }

    /// <summary>
    /// Sets the log level from the project settings.
    /// </summary>
    internal static void InitializeFromSettings()
    {
        CurrentLevel = IAPSettings.GetLogLevel();
    }
}
