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
    public const string EnableSteam = SettingBase + "providers/enable_steam";
    public const string EnableStoreKit = SettingBase + "providers/enable_storekit";
    public const string EnableGooglePlay = SettingBase + "providers/enable_google_play";
    public const string EnableLocalDebugProvider = SettingBase + "providers/enable_local_debug_provider";

    // Cache
    public const string CacheValiditySeconds = SettingBase + "cache/validity_seconds";
    public const string VerifyOnLaunch = SettingBase + "cache/verify_on_launch";

    // Logging
    public const string LogLevelSetting = SettingBase + "logging/log_level";

    // Code Generation
    public const string ConstantsOutputPath = SettingBase + "code_generation/constants_output_path";
    public const string ConstantsClassName = SettingBase + "code_generation/constants_class_name";
    public const string ConstantsNamespace = SettingBase + "code_generation/constants_namespace";

    // Default values
    private const string DefaultCatalogPath = "res://products.tres";
    private const int DefaultCacheValiditySeconds = 3600;
    private const bool DefaultVerifyOnLaunch = true;
    private const string DefaultConstantsOutputPath = "res://IAPConstants.cs";
    private const string DefaultConstantsClassName = "IAPConstants";

    /// <summary>
    /// Gets the path to the product catalog resource.
    /// </summary>
    public static string GetCatalogPath()
    {
        return GetSetting(CatalogPath, DefaultCatalogPath);
    }

    /// <summary>
    /// Gets whether Steam provider is enabled.
    /// </summary>
    public static bool IsSteamEnabled()
    {
        return GetSetting(EnableSteam, false);
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
    /// Gets how long the cache is considered valid (in seconds).
    /// 0 means always verify with provider.
    /// </summary>
    public static int GetCacheValiditySeconds()
    {
        return GetSetting(CacheValiditySeconds, DefaultCacheValiditySeconds);
    }

    /// <summary>
    /// Gets whether to verify ownership with provider on app launch.
    /// </summary>
    public static bool ShouldVerifyOnLaunch()
    {
        return GetSetting(VerifyOnLaunch, DefaultVerifyOnLaunch);
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
    /// Gets the output path for generated constants.
    /// </summary>
    public static string GetConstantsOutputPath()
    {
        return GetSetting(ConstantsOutputPath, DefaultConstantsOutputPath);
    }

    /// <summary>
    /// Gets the class name for generated constants.
    /// </summary>
    public static string GetConstantsClassName()
    {
        return GetSetting(ConstantsClassName, DefaultConstantsClassName);
    }

    /// <summary>
    /// Gets the namespace for generated constants (empty for no namespace).
    /// </summary>
    public static string GetConstantsNamespace()
    {
        return GetSetting(ConstantsNamespace, string.Empty);
    }

    /// <summary>
    /// Registers all IAP settings with default values.
    /// Called when the plugin is enabled.
    /// </summary>
    public static void RegisterSettings()
    {
        RegisterSetting(CatalogPath, DefaultCatalogPath, PropertyHint.File, "*.tres");
        RegisterSetting(EnableSteam, false);
        RegisterSetting(EnableStoreKit, false);
        RegisterSetting(EnableGooglePlay, false);
        RegisterSetting(EnableLocalDebugProvider, true);
        RegisterSetting(CacheValiditySeconds, DefaultCacheValiditySeconds);
        RegisterSetting(VerifyOnLaunch, DefaultVerifyOnLaunch);
        RegisterSetting(LogLevelSetting, (int)LogLevel.Info, PropertyHint.Enum, "None,Error,Warning,Info,Debug");
        RegisterSetting(ConstantsOutputPath, DefaultConstantsOutputPath, PropertyHint.SaveFile, "*.cs");
        RegisterSetting(ConstantsClassName, DefaultConstantsClassName);
        RegisterSetting(ConstantsNamespace, string.Empty);
    }

    /// <summary>
    /// Removes all IAP settings.
    /// Called when the plugin is disabled.
    /// </summary>
    public static void UnregisterSettings()
    {
        RemoveSetting(CatalogPath);
        RemoveSetting(EnableSteam);
        RemoveSetting(EnableStoreKit);
        RemoveSetting(EnableGooglePlay);
        RemoveSetting(EnableLocalDebugProvider);
        RemoveSetting(CacheValiditySeconds);
        RemoveSetting(VerifyOnLaunch);
        RemoveSetting(LogLevelSetting);
        RemoveSetting(ConstantsOutputPath);
        RemoveSetting(ConstantsClassName);
        RemoveSetting(ConstantsNamespace);
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
