#if TOOLS
using Godot;
using Godot.InAppPurchases.Core;

namespace Godot.InAppPurchases;

/// <summary>
/// Main editor plugin for Godot In-App Purchases.
/// Handles plugin lifecycle, autoload registration, and editor dock.
/// </summary>
[Tool]
public partial class IAPPlugin : EditorPlugin
{
    private const string AutoloadName = "IAPManager";
    private const string AutoloadPath = "res://addons/Godot.InAppPurchases.Net/Core/IAPManager.cs";

    // Editor dock will be added in Phase 2
    // private Control? _editorDock;

    public override void _EnterTree()
    {
        GD.Print("[IAP Plugin] Entering tree");
    }

    public override void _ExitTree()
    {
        GD.Print("[IAP Plugin] Exiting tree");
        // Remove editor dock when implemented
        // if (_editorDock != null)
        // {
        //     RemoveControlFromDocks(_editorDock);
        //     _editorDock.QueueFree();
        //     _editorDock = null;
        // }
    }

    public override void _EnablePlugin()
    {
        GD.Print("[IAP Plugin] Enabling plugin");

        // Register project settings
        IAPSettings.RegisterSettings();

        // Add autoload
        AddAutoloadSingleton(AutoloadName, AutoloadPath);

        GD.Print("[IAP Plugin] Plugin enabled");
    }

    public override void _DisablePlugin()
    {
        GD.Print("[IAP Plugin] Disabling plugin");

        // Remove autoload
        RemoveAutoloadSingleton(AutoloadName);

        // Note: We don't unregister settings on disable to preserve user configuration
        // IAPSettings.UnregisterSettings();

        GD.Print("[IAP Plugin] Plugin disabled");
    }

    public override void _SaveExternalData()
    {
        // Save any pending changes to the product catalog
        // This will be implemented with the editor dock in Phase 2
    }
}
#endif
