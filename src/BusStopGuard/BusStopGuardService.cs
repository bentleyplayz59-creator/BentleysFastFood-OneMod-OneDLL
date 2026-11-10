using System;
using System.Linq;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.BusStopGuard;

/// <summary>
/// Keeps the restaurant on the Bus Stop in place (re-requests Maps/BusStop when the Content Patcher
/// relocation is missing) and clears the trees/bushes that grow on the restaurant lawn.
/// This is the old standalone BusStopGuard DLL, now a service of the one mod. It has no event
/// subscriptions of its own: ModEntry forwards the events.
/// </summary>
internal sealed class BusStopGuardService
{
    private const string BusStopName = "BusStop";
    private const string BusStopAsset = "Maps/BusStop";

    private static readonly (int X, int Y, string Contains)[] Probes =
    {
        (33, 15, "BentleysRestaurant"),
        (34, 15, "BentleysRestaurant"),
        (37, 15, "Bentley's Fast Food")
    };

    private readonly ModContext context;

    private int pendingVerifyTicks = -1;
    private bool clearedThisVisit;
    private int lastClearDay = -1;
    private bool forceClear;
    private int attemptDay = -1;
    private int attemptsToday;
    private bool warnedCap;

    private BusStopGuardConfig Config => this.context.Config.BusStopGuard;

    private bool Active =>
        this.Config.Enabled
        && Context.IsWorldReady
        && this.context.Helper.ModRegistry.IsLoaded(ModContext.ContentPackId);

    public BusStopGuardService(ModContext context)
    {
        this.context = context;
    }

    public void Start()
    {
        this.context.Monitor.Log("Bus Stop guard started (part of Bentley's Fast Food).", LogLevel.Debug);
    }

    // ---- events, forwarded by ModEntry ----

    public void OnSaveLoaded()
    {
        this.ResetState();
        this.pendingVerifyTicks = 120;
    }

    public void OnReturnedToTitle()
    {
        this.ResetState();
    }

    public void OnDayStarted()
    {
        if (!this.Active)
            return;

        this.attemptsToday = 0;
        this.warnedCap = false;

        if (Game1.year > 1 && Game1.season == Season.Spring && Game1.dayOfMonth == 1)
            this.context.Monitor.Log("New in-game year detected: will verify the Bus Stop relocation.", LogLevel.Info);

        this.pendingVerifyTicks = 120;
    }

    public void OnAssetReady(AssetReadyEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(BusStopAsset, false) && this.Active)
            this.pendingVerifyTicks = Math.Max(this.pendingVerifyTicks, 10);
    }

    public void OnWarped(WarpedEventArgs e)
    {
        if (!e.IsLocalPlayer || !this.Active)
            return;

        if (e.NewLocation?.NameOrUniqueName == BusStopName)
        {
            this.clearedThisVisit = false;
            this.pendingVerifyTicks = Math.Max(this.pendingVerifyTicks, 5);
        }
    }

    public void OnUpdateTicked(UpdateTickedEventArgs e)
    {
        if (!this.Active)
            return;

        if (this.pendingVerifyTicks >= 0 && --this.pendingVerifyTicks < 0)
            this.VerifyAndRecover("scheduled check");

        if (this.InBusStop() && e.IsMultipleOf((uint)Math.Max(10, this.Config.CheckIntervalTicks)))
        {
            if (this.pendingVerifyTicks < 0 && !this.IsRelocationApplied())
                this.VerifyAndRecover("relocation missing while in Bus Stop");
            else
                this.TryClear();
        }
    }

    // ---- logic ----

    private void ResetState()
    {
        this.pendingVerifyTicks = -1;
        this.clearedThisVisit = false;
        this.lastClearDay = -1;
        this.forceClear = false;
        this.attemptDay = -1;
        this.attemptsToday = 0;
        this.warnedCap = false;
    }

    private bool InBusStop()
    {
        return Game1.currentLocation?.NameOrUniqueName == BusStopName;
    }

    private bool IsRelocationApplied()
    {
        GameLocation? busStop = Game1.getLocationFromName(BusStopName);
        if (busStop?.Map == null)
            return false;

        foreach ((int x, int y, string contains) in Probes)
        {
            string? action = busStop.doesTileHaveProperty(x, y, "Action", "Buildings", false);
            if (action == null || !action.Contains(contains))
                return false;
        }

        return true;
    }

    private void VerifyAndRecover(string reason)
    {
        if (this.IsRelocationApplied())
        {
            if (this.Config.VerboseLogging)
                this.context.Monitor.Log($"Relocation intact ({reason}).", LogLevel.Debug);
            return;
        }

        int totalDays = Game1.Date.TotalDays;
        if (this.attemptDay != totalDays)
        {
            this.attemptDay = totalDays;
            this.attemptsToday = 0;
        }

        if (this.attemptsToday >= this.Config.MaxRecoveryAttemptsPerDay)
        {
            if (!this.warnedCap)
            {
                this.context.Monitor.Log($"Relocation still missing after {this.attemptsToday} reload attempts today; giving up until tomorrow.", LogLevel.Warn);
                this.warnedCap = true;
            }
            return;
        }

        this.attemptsToday++;
        this.context.Monitor.Log($"Bus Stop relocation missing ({reason}). Reloading {BusStopAsset} so Content Patcher re-applies it (attempt {this.attemptsToday}).", LogLevel.Warn);
        this.context.Helper.GameContent.InvalidateCache(BusStopAsset);
        this.forceClear = true;
        this.pendingVerifyTicks = 45;
    }

    private void TryClear()
    {
        if (!Context.IsMainPlayer || !Context.IsPlayerFree || this.clearedThisVisit || this.pendingVerifyTicks >= 0 || !this.IsRelocationApplied())
            return;

        int totalDays = Game1.Date.TotalDays;
        if (!this.forceClear && this.Config.ClearOncePerDay && this.lastClearDay == totalDays)
        {
            this.clearedThisVisit = true;
            return;
        }

        GameLocation? location = Game1.currentLocation;
        if (location != null && location.NameOrUniqueName == BusStopName)
        {
            this.ClearLocation(location);
            this.clearedThisVisit = true;
            this.lastClearDay = totalDays;
            this.forceClear = false;
        }
    }

    private void ClearLocation(GameLocation location)
    {
        int trees = 0;
        foreach (Vector2 tile in location.terrainFeatures.Keys.ToList())
        {
            if (location.terrainFeatures[tile] is Tree)
            {
                location.terrainFeatures.Remove(tile);
                trees++;
            }
        }

        int bushes = 0;
        foreach (LargeTerrainFeature feature in location.largeTerrainFeatures.Where(f => f is Bush).ToList())
        {
            location.largeTerrainFeatures.Remove(feature);
            bushes++;
        }

        this.context.Monitor.Log($"Removed {trees} tree(s) and {bushes} bush(es) from the Bus Stop.", LogLevel.Info);
    }
}
