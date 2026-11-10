namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.BusStopGuard;

/// <summary>Settings of the Bus Stop guard. Stored in the "BusStopGuard" section of Code/config.json.</summary>
internal sealed class BusStopGuardConfig
{
    public bool Enabled { get; set; } = true;

    public bool ClearOncePerDay { get; set; } = true;

    public int CheckIntervalTicks { get; set; } = 60;

    public int MaxRecoveryAttemptsPerDay { get; set; } = 3;

    public bool VerboseLogging { get; set; }
}
