using System;
using System.Collections.Generic;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Announcement;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.BusStopGuard;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.FastFood;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Kiosk;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi;

/// <summary>
/// The ONE SMAPI entry point of Bentley's Fast Food & Useful Stuff.
/// It owns every event subscription and console command and forwards them to the services:
///   FastFood/   DoorController, WindexDrinkController, CounterMenuService
///   Kiosk/      KioskService (the old Kiosk DLL)
///   BusStopGuard/  BusStopGuardService (the old BusStopGuard DLL)
///   Announcement/  update announcement
/// </summary>
internal sealed class ModEntry : Mod
{
    private sealed class AnnouncementState
    {
        public Dictionary<string, string> SeenBySave { get; set; } = new();
    }

    private const string StateFile = "announcement-state";
    private const string LegacyBusStopGuardConfig = "BusStopGuard.config.json";

    private ModContext context = null!;
    private DoorController doors = null!;
    private WindexDrinkController windexDrink = null!;
    private CounterMenuService counter = null!;
    private KioskService kiosk = null!;
    private BusStopGuardService busStopGuard = null!;

    private AnnouncementState state = new();
    private bool announcementPending;

    public override void Entry(IModHelper helper)
    {
        this.context = new ModContext(helper, this.Monitor, this.ModManifest);
        DevLog.Monitor = this.Monitor;

        // Config: Code/config.json (outside the DLL). Old BusStopGuard.config.json values are carried over once.
        this.MigrateLegacyBusStopGuardConfig();
        this.context.ReloadConfig();
        this.context.SaveConfig(); // writes any new settings into config.json

        DevLog.Say("=== Bentley's Fast Food loaded (one mod, one DLL). Every mod action is logged with the [DEV] tag. ===");
        DevLog.Say("mod version = ", this.ModManifest.Version.ToString());
        DevLog.Say("config loaded: WindexDrinkDamage = ", this.context.Config.WindexDrinkDamage);
        DevLog.Say("config loaded: WindexSwirlConeDamage = ", this.context.Config.WindexSwirlConeDamage);

        this.ReloadAnnouncement();

        // Services. None of them subscribes to events itself; this class does it once, below.
        this.doors = new DoorController(helper, this.Monitor);
        this.windexDrink = new WindexDrinkController(this.context);
        this.counter = new CounterMenuService(this.context);
        this.kiosk = new KioskService(this.context);
        this.busStopGuard = new BusStopGuardService(this.context);

        this.counter.Register();
        this.busStopGuard.Start();

        // Events: exactly one subscription per event.
        helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
        helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        helper.Events.GameLoop.DayStarted += this.OnDayStarted;
        helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        helper.Events.Player.Warped += this.OnWarped;
        helper.Events.Content.AssetReady += this.OnAssetReady;

        // Console commands (existing names unchanged, one new one).
        helper.ConsoleCommands.Add("bentley_announcement", "Bentley Playz: 'bentley_announcement show' opens the update announcement now; 'bentley_announcement reset' makes it appear again next time this save is loaded.", this.OnAnnouncementCommand);
        helper.ConsoleCommands.Add("bentley_windex_test", "Bentley Playz DEV: 'bentley_windex_test' pretends you drank Windex; 'bentley_windex_test cone' pretends you ate the Windex Swirl Cone. Tests the damage code without the eating detection.", this.OnWindexTest);
        helper.ConsoleCommands.Add("bentley_reload_config", "Bentley Playz: re-reads Code/config.json without restarting the game.", this.OnReloadConfigCommand);

        helper.ConsoleCommands.Add("bentley_reset_kiosk", "Bentley Playz DEV: takes the kiosk letter, the kiosk recipe and the 2-heart kiosk event out of this save's history so you can test them again. Then set Bentley to 2 hearts and sleep.", this.OnResetKioskCommand);

        DevLog.Say("Entry finished: event handlers and console commands registered (bentley_announcement, bentley_windex_test, bentley_reload_config)");
    }

    // ---- config ----

    /// <summary>If the old standalone BusStopGuard.config.json exists and config.json has no BusStopGuard section yet, copy the old values over.</summary>
    private void MigrateLegacyBusStopGuardConfig()
    {
        try
        {
            BusStopGuardConfig? legacy = this.Helper.Data.ReadJsonFile<BusStopGuardConfig>(LegacyBusStopGuardConfig);
            if (legacy == null)
                return;

            var raw = this.Helper.Data.ReadJsonFile<Dictionary<string, object>>("config.json");
            if (raw != null && raw.ContainsKey(nameof(ModConfig.BusStopGuard)))
                return;

            ModConfig config = this.Helper.ReadConfig<ModConfig>();
            config.BusStopGuard = legacy;
            this.Helper.WriteConfig(config);
            this.Monitor.Log($"Copied your old {LegacyBusStopGuardConfig} settings into config.json (section 'BusStopGuard'). The old file is no longer used.", LogLevel.Info);
        }
        catch (Exception ex)
        {
            this.Monitor.Log($"Could not migrate {LegacyBusStopGuardConfig}: {ex.Message}", LogLevel.Warn);
        }
    }

    private void OnReloadConfigCommand(string command, string[] args)
    {
        this.context.ReloadConfig();
        this.Monitor.Log("config.json reloaded.", LogLevel.Info);
    }

    // ---- events ----

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        DevLog.Say("GameLaunched event fired");

        bool packLoaded = this.Helper.ModRegistry.IsLoaded(ModContext.ContentPackId);
        DevLog.Say("Content Patcher pack BentleyPlayz.FastFoodUsefulStuff loaded = ", packLoaded);
        if (!packLoaded)
            this.Monitor.Log("The Content Patcher pack 'BentleyPlayz.FastFoodUsefulStuff' is not loaded. The doors, counter menu, kiosk, Windex and announcement need it; install both folders (Code and Content).", LogLevel.Warn);
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        DevLog.Say("SaveLoaded event fired");

        this.ReloadAnnouncement();
        this.state = this.Helper.Data.ReadGlobalData<AnnouncementState>(StateFile) ?? new AnnouncementState();
        this.announcementPending = !this.HasSeenAnnouncement();
        this.Monitor.Log(this.announcementPending ? "Update announcement will be shown once the world is ready." : "Update announcement already seen for this save.", LogLevel.Info);

        this.busStopGuard.OnSaveLoaded();
    }

    private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
    {
        DevLog.Say("ReturnedToTitle event fired");
        this.announcementPending = false;
        this.busStopGuard.OnReturnedToTitle();
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        this.busStopGuard.OnDayStarted();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        this.windexDrink.OnUpdateTicked();
        this.kiosk.OnUpdateTicked();
        DevLog.SafeTick();
        this.busStopGuard.OnUpdateTicked(e);

        if (this.announcementPending && e.IsMultipleOf(30))
            this.TryShowAnnouncement();
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        // The shop picker (counter / kiosk) must always close on the back button, Esc, B or the phone's back key.
        if (ChoiceMenu.TryHandleBackButton(e, this.Helper))
            return;

        this.doors.OnButtonPressed(e);
        this.kiosk.OnButtonPressed(e);
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        this.busStopGuard.OnWarped(e);
    }

    private void OnAssetReady(object? sender, AssetReadyEventArgs e)
    {
        this.busStopGuard.OnAssetReady(e);
    }

    // ---- announcement ----

    private void ReloadAnnouncement()
    {
        AnnouncementContent.Load(this.Helper, this.Monitor, this.ModManifest.Version.ToString());
    }

    private void TryShowAnnouncement()
    {
        if (Context.IsWorldReady && Context.IsPlayerFree && Game1.activeClickableMenu == null && Game1.CurrentEvent == null && !Game1.isFestival())
        {
            this.announcementPending = false;
            DevLog.Say("showing the update announcement now");
            this.ShowAnnouncement();
        }
    }

    private void ShowAnnouncement()
    {
        string saveKey = this.GetSaveKey();
        string version = AnnouncementContent.Version;
        Game1.activeClickableMenu = new AnnouncementMenu(Game1.player.Name, () => this.MarkAnnouncementSeen(saveKey, version));
    }

    private bool HasSeenAnnouncement()
    {
        return this.state.SeenBySave.TryGetValue(this.GetSaveKey(), out string? seen) && seen == AnnouncementContent.Version;
    }

    private void MarkAnnouncementSeen(string saveKey, string version)
    {
        this.state.SeenBySave[saveKey] = version;
        this.Helper.Data.WriteGlobalData(StateFile, this.state);
        this.Monitor.Log($"Announcement {version} dismissed for '{saveKey}'.", LogLevel.Info);
    }

    private string GetSaveKey()
    {
        return $"{Constants.SaveFolderName}/{Game1.player.UniqueMultiplayerID}";
    }

    // ---- console commands ----

    private void OnAnnouncementCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.Monitor.Log("Load a save first.", LogLevel.Warn);
            return;
        }

        string action = args.Length != 0 ? args[0].ToLowerInvariant() : "show";
        if (action == "reset")
        {
            this.state.SeenBySave.Remove(this.GetSaveKey());
            this.Helper.Data.WriteGlobalData(StateFile, this.state);
            this.Monitor.Log("Announcement reset: it will appear the next time this save is loaded.", LogLevel.Info);
        }
        else if (action == "show")
        {
            this.ReloadAnnouncement();
            this.ShowAnnouncement();
        }
        else
        {
            this.Monitor.Log("Usage: bentley_announcement show | reset", LogLevel.Info);
        }
    }

    private void OnResetKioskCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            this.Monitor.Log("Load a save first.", LogLevel.Warn);
            return;
        }

        Farmer player = Game1.player;
        string letter = ModContext.ContentPackId + "_KioskLetter";
        string recipe = ModContext.ContentPackId + "_RestaurantKiosk";
        string intro = ModContext.ContentPackId + "_KioskIntro";

        player.mailReceived.Remove(letter);
        player.mailForTomorrow.Remove(letter);
        player.mailbox.Remove(letter);
        player.craftingRecipes.Remove(recipe);
        player.eventsSeen.Remove(intro);
        player.modData.Remove(KioskService.DiscountDataKey);

        this.Monitor.Log("Kiosk letter, kiosk recipe, kiosk event and today's kiosk deals reset. Set Bentley to 2 hearts, then sleep.", LogLevel.Info);
    }

    private void OnWindexTest(string command, string[] args)
    {
        WindexDrinkController.lastWasCone = args.Length >= 1 && args[0] == "cone";
        DevLog.Say("console test: pretending you just consumed an item. is cone = ", WindexDrinkController.lastWasCone);

        if (!Context.IsWorldReady)
            DevLog.Say("console test needs a loaded save (world not ready)");
        else
            this.windexDrink.ApplyDamage(Game1.player);
    }
}
