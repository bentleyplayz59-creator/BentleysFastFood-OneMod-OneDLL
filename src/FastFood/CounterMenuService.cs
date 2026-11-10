using System;
using System.Collections.Generic;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.FastFood;

/// <summary>
/// The ordering counter (tiles 14-18, row 5 of the restaurant). Pressing the action button asks
/// "What would you like to see?" and opens Main Menu, Breakfast or Seasonal.
/// The counter tiles carry the tile action <see cref="ModContext.CounterMenuAction"/> (set in Content/data/shop.json).
/// </summary>
internal sealed class CounterMenuService
{
    private const string AnswerMain = "main";
    private const string AnswerBreakfast = "breakfast";
    private const string AnswerSeasonal = "seasonal";
    private const string AnswerCancel = "Esc";

    private readonly ModContext context;

    public CounterMenuService(ModContext context)
    {
        this.context = context;
    }

    public void Register()
    {
        GameLocation.RegisterTileAction(ModContext.CounterMenuAction, this.HandleCounterAction);
    }

    private bool HandleCounterAction(GameLocation location, string[] args, Farmer who, Microsoft.Xna.Framework.Point tile)
    {
        if (!ReferenceEquals(who, Game1.player))
            return false;

        DevLog.Say("counter action used at tile ", tile.X + "," + tile.Y);

        List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(AnswerMain, "Main Menu"),
            new KeyValuePair<string, string>(AnswerBreakfast, "Breakfast"),
            new KeyValuePair<string, string>(AnswerSeasonal, "Seasonal"),
            new KeyValuePair<string, string>(AnswerCancel, "Never mind")
        };

        // ChoiceMenu (not the vanilla question box) so the back button, the X and a tap outside always close it.
        ChoiceMenu.Show("What would you like to see?", choices, answer => this.OnAnswer(Game1.player, answer));
        return true;
    }

    private void OnAnswer(Farmer who, string answer)
    {
        DevLog.Say("counter menu answer = ", answer);

        switch (answer)
        {
            case AnswerMain:
                this.OpenShop(ModContext.MainShopId);
                break;

            case AnswerBreakfast:
                if (this.IsBreakfastTime())
                    this.OpenShop(ModContext.BreakfastShopId);
                else
                    Game1.drawObjectDialogue(this.BreakfastClosedMessage());
                break;

            case AnswerSeasonal:
                this.OpenShop(ModContext.SeasonalShopId);
                break;

            // AnswerCancel / anything else: close the dialogue and do nothing
        }
    }

    internal bool IsBreakfastTime()
    {
        ModConfig config = this.context.Config;
        int now = Game1.timeOfDay;
        return now >= config.BreakfastStartTime && now < config.BreakfastEndTime;
    }

    internal string BreakfastClosedMessage()
    {
        ModConfig config = this.context.Config;
        return $"Breakfast is served from {FormatTime(config.BreakfastStartTime)} to {FormatTime(config.BreakfastEndTime)}.";
    }

    /// <summary>600 -> "6 AM", 1030 -> "10:30 AM", 1300 -> "1 PM".</summary>
    internal static string FormatTime(int gameTime)
    {
        int hour = gameTime / 100 % 24;
        int minute = gameTime % 100;
        string suffix = hour >= 12 ? "PM" : "AM";
        int shown = hour % 12;
        if (shown == 0)
            shown = 12;
        return minute == 0 ? $"{shown} {suffix}" : $"{shown}:{minute:00} {suffix}";
    }

    private void OpenShop(string shopId)
    {
        try
        {
            if (!Utility.TryOpenShopMenu(shopId, (string?)null, true))
            {
                this.context.Monitor.Log($"Shop '{shopId}' could not be opened. Is the Content part installed?", LogLevel.Error);
                Game1.drawObjectDialogue("The counter is closed right now.");
            }
        }
        catch (Exception ex)
        {
            this.context.Monitor.Log($"Could not open counter shop '{shopId}': {ex}", LogLevel.Error);
        }
    }
}
