using BentleyPlayz.FastFoodUsefulStuff.Smapi.BusStopGuard;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;

/// <summary>
/// The one config for the whole mod. It lives in Code/config.json (next to the DLL, not inside it),
/// so you can edit it without recompiling. SMAPI creates the file with these defaults and fills in
/// any missing setting the next time the game starts.
/// </summary>
internal sealed class ModConfig
{
    // ---- Windex (existing settings, names unchanged) ----

    /// <summary>Health lost when you drink Windex. 0 = none.</summary>
    public int WindexDrinkDamage { get; set; } = 10;

    /// <summary>Health lost when you eat the Windex Swirl Cone. 0 = none.</summary>
    public int WindexSwirlConeDamage { get; set; } = 10;

    // ---- Ordering counter menu ----
    // (The counter menu and the update announcement are always on; they are not settings.)

    /// <summary>Breakfast starts at this game time (600 = 6:00 AM).</summary>
    public int BreakfastStartTime { get; set; } = 600;

    /// <summary>Breakfast stops being served at this game time (1000 = 10:00 AM).</summary>
    public int BreakfastEndTime { get; set; } = 1000;

    // ---- Restaurant Kiosk ----

    /// <summary>How many purchased units per day get the kiosk discount.</summary>
    public int KioskDealsPerDay { get; set; } = 3;

    /// <summary>Kiosk discount in percent.</summary>
    public int KioskDiscountPercent { get; set; } = 10;

    // ---- Logging ----

    /// <summary>true = write the extra [DEV] lines to the SMAPI log (the behaviour of the old DEV build).</summary>
    public bool DevLogging { get; set; } = true;

    // ---- Bus Stop guard (was BusStopGuard.config.json) ----

    public BusStopGuardConfig BusStopGuard { get; set; } = new();

    /// <summary>Clamp values that would break things.</summary>
    public void Normalize()
    {
        this.WindexDrinkDamage = System.Math.Max(0, this.WindexDrinkDamage);
        this.WindexSwirlConeDamage = System.Math.Max(0, this.WindexSwirlConeDamage);
        this.BreakfastStartTime = System.Math.Clamp(this.BreakfastStartTime, 0, 2600);
        this.BreakfastEndTime = System.Math.Clamp(this.BreakfastEndTime, 0, 2600);
        this.KioskDealsPerDay = System.Math.Max(0, this.KioskDealsPerDay);
        this.KioskDiscountPercent = System.Math.Clamp(this.KioskDiscountPercent, 0, 100);
        this.BusStopGuard ??= new BusStopGuardConfig();
    }
}
