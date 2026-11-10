using System;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using StardewModdingAPI;
using StardewValley;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.FastFood;

/// <summary>Health damage when the farmer drinks Windex or eats the Windex Swirl Cone.</summary>
internal sealed class WindexDrinkController
{
    public const string WindexQualifiedId = "(O)BentleyPlayz.FastFoodUsefulStuff_Windex";
    public const string WindexConeQualifiedId = "(O)BentleyPlayz.FastFoodUsefulStuff_WindexSwirlCone";

    private readonly ModContext context;
    private bool drinkingWindex;

    internal static bool lastWasCone;

    public WindexDrinkController(ModContext context)
    {
        this.context = context;
    }

    public void OnUpdateTicked()
    {
        if (!Context.IsWorldReady)
        {
            this.drinkingWindex = false;
            return;
        }

        Farmer player = Game1.player;
        if (player.isEating)
        {
            if (!this.drinkingWindex && IsHarmfulItem(player.itemToEat?.QualifiedItemId))
            {
                DevLog.Say("DETECTED Windex/cone being consumed. is cone = ", lastWasCone);
                this.drinkingWindex = true;
            }
        }
        else if (this.drinkingWindex)
        {
            DevLog.Say("finished consuming a Windex item, applying damage now");
            this.drinkingWindex = false;
            this.ApplyDamage(player);
        }
    }

    internal void ApplyDamage(Farmer who)
    {
        try
        {
            DevLog.Say("ApplyDamage called. item is cone = ", lastWasCone);

            int damage = Math.Max(0, lastWasCone ? this.context.Config.WindexSwirlConeDamage : this.context.Config.WindexDrinkDamage);
            DevLog.Say("damage setting used = ", damage);

            if (damage == 0)
            {
                DevLog.Say("damage setting is 0 so no damage is applied");
                return;
            }

            int before = who.health;
            who.health = Math.Max(1, before - damage);
            DevLog.Say("health before -> after damage: ", before, who.health);

            int lost = before - who.health;
            Game1.addHUDMessage(new HUDMessage("Ouch! Windex hurt you for " + lost + " health.", HUDMessage.error_type));
            this.context.Monitor.Log($"Windex drunk: {lost} health lost (setting {damage}).", LogLevel.Info);
        }
        catch (Exception ex)
        {
            this.context.Monitor.Log($"Windex drinking damage failed: {ex}", LogLevel.Error);
        }
    }

    private static bool IsHarmfulItem(string? id)
    {
        lastWasCone = id == WindexConeQualifiedId;
        return id == WindexQualifiedId || lastWasCone;
    }
}
