using Godot;

namespace Godot.InAppPurchases.Core;

/// <summary>
/// Provides access to IAP project settings.
/// </summary>
public static class IAPSettings
{
    // Setting paths
    private const string SettingBase = "addons/iap/";

    // Catalog
    public const string CatalogPath = SettingBase + "catalog_path";

    // Providers
    public const string EnableStoreKit = SettingBase + "providers/enable_storekit";
    public const string EnableGooglePlay = SettingBase + "providers/enable_google_play";
    public const string EnableLocalDebugProvider = SettingBase + "providers/enable_local_debug_provider";

    // Logging
    public const string LogLevelSetting = SettingBase + "logging/log_level";

    // Default values
    private const string DefaultCatalogPath = "res://products.tres";

    /// <summary>
    /// Gets the path to the product catalog resource.
    /// </summary>
    public static string GetCatalogPath()
    {
        return GetSetting(CatalogPath, DefaultCatalogPath);
    }

    /// <summary>
    /// Gets whether StoreKit (iOS) provider is enabled.
    /// </summary>
    public static bool IsStoreKitEnabled()
    {
        return GetSetting(EnableStoreKit, false);
    }

    /// <summary>
    /// Gets whether Google Play provider is enabled.
    /// </summary>
    public static bool IsGooglePlayEnabled()
    {
        return GetSetting(EnableGooglePlay, false);
    }

    /// <summary>
    /// Gets whether local debug provider is enabled.
    /// </summary>
    public static bool IsLocalDebugProviderEnabled()
    {
        return GetSetting(EnableLocalDebugProvider, true);
    }

    /// <summary>
    /// Gets the current log level.
    /// </summary>
    public static LogLevel GetLogLevel()
    {
        var level = GetSetting(LogLevelSetting, (int)LogLevel.Info);
        return (LogLevel)level;
    }

    /// <summary>
    /// Registers all IAP settings with default values.
    /// Called when the plugin is enabled.
    /// </summary>
    public static void RegisterSettings()
    {
        RegisterSetting(CatalogPath, DefaultCatalogPath, PropertyHint.File, "*.tres");
        RegisterSetting(EnableStoreKit, false);
        RegisterSetting(EnableGooglePlay, false);
        RegisterSetting(EnableLocalDebugProvider, true);
        RegisterSetting(LogLevelSetting, (int)LogLevel.Info, PropertyHint.Enum, "None,Error,Warning,Info,Debug");
    }

    /// <summary>
    /// Removes all IAP settings.
    /// Called when the plugin is disabled.
    /// </summary>
    public static void UnregisterSettings()
    {
        RemoveSetting(CatalogPath);
        RemoveSetting(EnableStoreKit);
        RemoveSetting(EnableGooglePlay);
        RemoveSetting(EnableLocalDebugProvider);
        RemoveSetting(LogLevelSetting);
    }

    private static T GetSetting<T>(string path, T defaultValue)
    {
        if (ProjectSettings.HasSetting(path))
        {
            var value = ProjectSettings.GetSetting(path);
            if (value.Obj is T typedValue)
            {
                return typedValue;
            }
            // Handle numeric conversions
            if (typeof(T) == typeof(int) && value.Obj is long longValue)
            {
                return (T)(object)(int)longValue;
            }
            if (typeof(T) == typeof(bool) && value.Obj is bool boolValue)
            {
                return (T)(object)boolValue;
            }
        }
        return defaultValue;
    }

    private static void RegisterSetting(string path, Variant defaultValue, PropertyHint hint = PropertyHint.None, string hintString = "")
    {
        if (!ProjectSettings.HasSetting(path))
        {
            ProjectSettings.SetSetting(path, defaultValue);
        }

        ProjectSettings.SetInitialValue(path, defaultValue);

        var propertyInfo = new Godot.Collections.Dictionary
        {
            { "name", path },
            { "type", (int)defaultValue.VariantType },
            { "hint", (int)hint },
            { "hint_string", hintString }
        };

        ProjectSettings.AddPropertyInfo(propertyInfo);
    }

    private static void RemoveSetting(string path)
    {
        if (ProjectSettings.HasSetting(path))
        {
            ProjectSettings.SetSetting(path, default);
        }
    }
}
