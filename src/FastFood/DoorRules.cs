namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.FastFood;

internal static class DoorRules
{
    public const int BedroomHeartsRequired = 3;

    public const string RestaurantClosedMessage = "Bentley's Fast Food is open 24 hours a day, every day.";

    public const string BedroomLockedMessage = "The door is locked. Bentley's bedroom is private. Maybe he'd let you in if you were better friends (3 hearts).";

    public static bool IsRestaurantOpen() => true;

    // Unchanged behaviour: the staff door is open to everyone (heart level >= 0).
    public static bool IsBedroomOpen(int heartLevel) => heartLevel >= 0;
}
