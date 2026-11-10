using System;
using StardewModdingAPI;
using StardewValley;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;

/// <summary>The "[DEV]" log lines described in DEV_LOGGING.md. Switch off with "DevLogging": false in config.json.</summary>
internal static class DevLog
{
    public static IMonitor? Monitor;
    public static bool Enabled = true;

    private static int tick;
    private static bool lastEating;
    private static bool lastWorld;
    private static int lastHealth;

    public static void Say(string message)
    {
        if (Enabled && Monitor != null)
            Monitor.Log("[DEV] " + message, LogLevel.Info);
    }

    public static void Say(string label, string? value) => Say(label + (value ?? "(null)"));

    public static void Say(string label, int value) => Say(label + value);

    public static void Say(string label, bool value) => Say(label + (value ? "true" : "false"));

    public static void Say(string label, int before, int after) => Say(label + before + " -> " + after);

    public static void SafeTick()
    {
        try
        {
            Tick();
        }
        catch (Exception)
        {
            // logging must never break the game
        }
    }

    private static void Tick()
    {
        if (!Enabled || Monitor == null)
            return;

        tick++;

        bool worldReady = Context.IsWorldReady;
        if (worldReady != lastWorld)
        {
            lastWorld = worldReady;
            Say("world ready = ", worldReady);
        }
        if (!worldReady)
            return;

        Farmer? player = Game1.player;
        if (player == null)
            return;

        int health = player.health;
        if (lastHealth != 0 && health != lastHealth)
            Say("player health changed: ", lastHealth, health);
        lastHealth = health;

        bool eating = player.isEating;
        if (eating != lastEating)
        {
            lastEating = eating;
            Say("isEating changed -> ", eating);
            if (eating)
            {
                Say("itemToEat = ", player.itemToEat?.QualifiedItemId ?? "(null)");
                Say("health when eating began = ", health);
            }
        }

        if (tick % 1800 == 0)
            Say("heartbeat (mod alive), ticks so far = ", tick);
    }
}
