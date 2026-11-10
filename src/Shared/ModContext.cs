using System;
using StardewModdingAPI;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;

/// <summary>Everything the services share: SMAPI helpers, the live config and the mod's IDs.</summary>
internal sealed class ModContext
{
    // ---- IDs (unchanged from the old DLLs) ----
    public const string ContentPackId = "BentleyPlayz.FastFoodUsefulStuff";
    public const string RestaurantLocation = "BentleyPlayz.FastFoodUsefulStuff_BentleysRestaurant";
    public const string BedroomLocation = "BentleyPlayz.FastFoodUsefulStuff_BentleysBedroom";
    public const string BentleyNpcName = "BentleyPlayz.FastFoodUsefulStuff_Bentley";

    // ---- Shops (defined in Content/data/*.json) ----
    public const string MainShopId = "BentleyPlayz.FastFoodUsefulStuff_BentleyShop";
    public const string BreakfastShopId = "BentleyPlayz.FastFoodUsefulStuff_BentleyBreakfast";
    public const string SeasonalShopId = "BentleyPlayz.FastFoodUsefulStuff_BentleySeasonal";
    public const string KioskFoodShopId = "BentleyPlayz.FastFoodUsefulStuff_BentleyMenu";
    public const string DessertShopId = "BentleyPlayz.FastFoodUsefulStuff_DessertKiosk";

    /// <summary>Tile action set on the ordering counter (Content/data/shop.json).</summary>
    public const string CounterMenuAction = "BentleyPlayz.FastFoodUsefulStuff_CounterMenu";

    public IModHelper Helper { get; }
    public IMonitor Monitor { get; }
    public IManifest Manifest { get; }

    /// <summary>The current config. Replaced as a whole by <see cref="ReloadConfig"/>, so always read it through this property.</summary>
    public ModConfig Config { get; private set; } = new();

    public ModContext(IModHelper helper, IMonitor monitor, IManifest manifest)
    {
        this.Helper = helper;
        this.Monitor = monitor;
        this.Manifest = manifest;
    }

    public void ReloadConfig()
    {
        ModConfig config;
        try
        {
            config = this.Helper.ReadConfig<ModConfig>();
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Could not read config.json, using defaults: {ex.Message}", LogLevel.Warn);
            config = new ModConfig();
        }

        config.Normalize();
        this.Config = config;
        DevLog.Enabled = config.DevLogging;
    }

    public void SaveConfig()
    {
        this.Helper.WriteConfig(this.Config);
    }
}
