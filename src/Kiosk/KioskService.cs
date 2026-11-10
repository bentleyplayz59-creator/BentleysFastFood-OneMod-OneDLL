using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Kiosk;

/// <summary>
/// Bentley's Restaurant Kiosk (the placeable big craftable).
/// Action button on a placed kiosk -> "Desserts, Food or Seasonal?" -> opens that shop with kiosk-only
/// deals (default: 10% off, 3 purchased units per day per player).
/// Ported from the decompiled source of the old Kiosk DLL (3.14.0). Behaviour, item ID, shop IDs and the
/// save-data key/format are unchanged; the only additions are the Seasonal choice and the config values.
/// 3.16.1: the shop picker is a ChoiceMenu (always closes with back), the kiosk is found from any side
/// (including a tap on its top half), and discounted items are stock-limited to the deals you have left.
/// </summary>
internal sealed class KioskService
{
    public const string KioskQualifiedId = "(BC)BentleyPlayz.FastFoodUsefulStuff_RestaurantKiosk";

    /// <summary>Per-player save data key (unchanged). Value = "totalDays:unitsUsedToday" (same as the old Kiosk DLL).</summary>
    public const string DiscountDataKey = "BentleyPlayz.FastFoodUsefulStuff/KioskDiscountDay";

    private const float MaxReachTiles = 2.5f;
    private const int OpenCooldownTicks = 20;

    private const string AnswerDesserts = "Desserts";
    private const string AnswerFood = "Food";
    private const string AnswerSeasonal = "Seasonal";
    private const string AnswerCancel = "Esc";

    /// <summary>What the kiosk remembers about the one shop window that is open right now.</summary>
    private sealed class DealSession
    {
        public ShopMenu Shop = null!;

        /// <summary>Every discounted entry exactly as the shop had it when it opened (full price, original stock).</summary>
        public readonly Dictionary<ISalable, ItemStockInformation> Original = new Dictionary<ISalable, ItemStockInformation>();

        /// <summary>The stock number the shop last had for each entry (what we set, or what the game counted down to).</summary>
        public readonly Dictionary<ISalable, int> LastStock = new Dictionary<ISalable, int>();

        /// <summary>Every entry the shop listed when we looked (discounted or not). A new entry we do not know means the shop rebuilt its list.</summary>
        public readonly HashSet<ISalable> Known = new HashSet<ISalable>();

        /// <summary>
        /// The FULL-price entry for each item id, recorded the first time the shop showed it. A rebuilt shop list
        /// can still hold entries we already discounted; this makes sure a discount is never taken off a discounted price.
        /// </summary>
        public readonly Dictionary<string, ItemStockInformation> OriginalById = new Dictionary<string, ItemStockInformation>();

        /// <summary>Units bought of each item id during this visit (by id, so it survives the shop rebuilding its list).</summary>
        public readonly Dictionary<string, int> Bought = new Dictionary<string, int>();
    }

    private readonly ModContext context;
    private int lastOpenTick = -100;
    private DealSession? session;

    public KioskService(ModContext context)
    {
        this.context = context;
    }

    public void OnButtonPressed(ButtonPressedEventArgs e)
    {
        try
        {
            this.HandleButton(e);
        }
        catch (Exception ex)
        {
            this.context.Monitor.Log($"Kiosk button handler failed: {ex}", LogLevel.Error);
        }
    }

    private void HandleButton(ButtonPressedEventArgs e)
    {
        // Same as the old DLL: the action button, or a left click (a tap on a phone is a left click).
        if ((!e.Button.IsActionButton() && e.Button != SButton.MouseLeft) || !Context.IsWorldReady || !Context.IsPlayerFree)
            return;

        GameLocation? location = Game1.currentLocation;
        if (location == null)
            return;

        Vector2? found = this.FindKioskTile(location, e);
        if (found == null)
        {
            this.LogMissedPress(location, e);
            return;
        }

        Vector2 tile = found.Value;
        float distance = Vector2.Distance(Game1.player.Tile, tile);
        if (distance > MaxReachTiles)
        {
            DevLog.Say("kiosk pressed but too far away, distance = " + distance.ToString("0.0", CultureInfo.InvariantCulture));
            return;
        }

        this.context.Helper.Input.Suppress(e.Button);

        // The same press can arrive twice (click + tap); open only once.
        if (Game1.ticks - this.lastOpenTick < OpenCooldownTicks)
        {
            DevLog.Say("kiosk press ignored (same press arrived twice)");
            return;
        }
        this.lastOpenTick = Game1.ticks;

        DevLog.Say("kiosk used at tile ", (int)tile.X + "," + (int)tile.Y);

        List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(AnswerDesserts, "Desserts"),
            new KeyValuePair<string, string>(AnswerFood, "Food"),
            new KeyValuePair<string, string>(AnswerSeasonal, "Seasonal"),
            new KeyValuePair<string, string>(AnswerCancel, "Cancel")
        };
        ChoiceMenu.Show("Desserts, Food or Seasonal?", choices, answer => this.OnChoice(Game1.player, answer));
    }

    /// <summary>
    /// Finds the kiosk the press was meant for. The kiosk object sits on ONE tile but its picture is two tiles tall,
    /// so a press on the top half of the picture lands on the tile above the object. Candidates, in order:
    /// the grab tile, the tile under the cursor / finger, and (action button only) the tile the player is facing.
    /// For each one we also accept the kiosk standing on the tile directly below it.
    /// </summary>
    private Vector2? FindKioskTile(GameLocation location, ButtonPressedEventArgs e)
    {
        List<Vector2> candidates = new List<Vector2> { e.Cursor.GrabTile, e.Cursor.Tile };
        if (e.Button.IsActionButton())
            candidates.Add(this.FacingTile());

        foreach (Vector2 candidate in candidates)
        {
            if (this.IsKioskAt(location, candidate))
                return candidate;

            Vector2 below = new Vector2(candidate.X, candidate.Y + 1);
            if (!location.Objects.ContainsKey(candidate) && this.IsKioskAt(location, below))
                return below;
        }

        return null;
    }

    private Vector2 FacingTile()
    {
        Vector2 tile = Game1.player.Tile;
        switch (Game1.player.FacingDirection)
        {
            case 0: tile.Y -= 1; break; // up
            case 1: tile.X += 1; break; // right
            case 2: tile.Y += 1; break; // down
            case 3: tile.X -= 1; break; // left
        }
        return tile;
    }

    /// <summary>Dev log only: a press happened next to a kiosk but did not find it. Shows the tiles that were tried.</summary>
    private void LogMissedPress(GameLocation location, ButtonPressedEventArgs e)
    {
        if (!DevLog.Enabled)
            return;

        foreach (KeyValuePair<Vector2, StardewValley.Object> pair in location.Objects.Pairs)
        {
            if (pair.Value == null || pair.Value.QualifiedItemId != KioskQualifiedId)
                continue;
            if (Vector2.Distance(Game1.player.Tile, pair.Key) > 3.5f)
                continue;

            DevLog.Say("kiosk NOT matched. button=" + e.Button
                + " kiosk=" + (int)pair.Key.X + "," + (int)pair.Key.Y
                + " player=" + (int)Game1.player.Tile.X + "," + (int)Game1.player.Tile.Y
                + " facing=" + Game1.player.FacingDirection
                + " grab=" + (int)e.Cursor.GrabTile.X + "," + (int)e.Cursor.GrabTile.Y
                + " cursor=" + (int)e.Cursor.Tile.X + "," + (int)e.Cursor.Tile.Y);
            return;
        }
    }

    private bool IsKioskAt(GameLocation location, Vector2 tile)
    {
        return location.Objects.TryGetValue(tile, out StardewValley.Object? obj)
            && obj != null
            && obj.QualifiedItemId == KioskQualifiedId;
    }

    private void OnChoice(Farmer who, string answer)
    {
        DevLog.Say("kiosk menu answer = ", answer);

        if (who == null || !who.IsLocalPlayer)
            return;

        string? shopId = answer switch
        {
            AnswerDesserts => ModContext.DessertShopId,
            AnswerFood => ModContext.KioskFoodShopId,
            AnswerSeasonal => ModContext.SeasonalShopId,
            _ => null
        };

        if (shopId != null)
            this.OpenShop(shopId);
    }

    private void OpenShop(string shopId)
    {
        try
        {
            this.session = null;

            bool opened = Utility.TryOpenShopMenu(shopId, (string?)null, true);
            DevLog.Say("kiosk opening shop " + shopId + ", opened = ", opened);
            if (!opened)
            {
                this.context.Monitor.Log($"Shop '{shopId}' could not be opened. Is the Content part installed?", LogLevel.Error);
                return;
            }

            if (Game1.activeClickableMenu is ShopMenu shop)
                this.ApplyDeals(shop);
        }
        catch (Exception ex)
        {
            this.context.Monitor.Log($"Could not open kiosk shop '{shopId}': {ex}", LogLevel.Error);
        }
    }

    // ---- deals ----
    //
    // How the daily limit works (3.16.1): while you have deals left, every discounted entry is capped to "deals left"
    // in stock, so the shop itself refuses to sell more than that (also with the phone's quantity buttons). Every tick we
    // look at how far the game counted the stock down, record the bought units against today's deals and lower the caps
    // of all entries to the new "deals left". When the last deal is used, prices and stock go back to normal.
    // This does not depend on the shop's onPurchase callback, which the phone version does not use the same way.

    private void ApplyDeals(ShopMenu shop)
    {
        int dealsLeft = this.DealsLeft();
        DevLog.Say("kiosk shop opened, deals left today = ", dealsLeft);

        // Out of deals: the shop stays open and works normally, at full price.
        if (dealsLeft <= 0)
        {
            Game1.addHUDMessage(new HUDMessage("No kiosk deals left today. Regular prices apply.", HUDMessage.achievement_type));
            return;
        }

        DealSession newSession = new DealSession { Shop = shop };
        this.Snapshot(newSession);

        if (newSession.Original.Count == 0)
            return;

        this.session = newSession;
        this.ApplyLimits(newSession, dealsLeft);

        foreach (KeyValuePair<ISalable, ItemStockInformation> entry in newSession.Original)
        {
            if (newSession.Shop.itemPriceAndStock.TryGetValue(entry.Key, out ItemStockInformation now))
                DevLog.Say("kiosk entry " + entry.Key.Name + ": price " + entry.Value.Price + " -> " + now.Price + ", stock " + (entry.Value.Stock == int.MaxValue ? "unlimited" : entry.Value.Stock.ToString()) + " -> " + now.Stock);
        }
        DevLog.Say("kiosk shop menu type = " + shop.GetType().Name + ", entries limited = ", newSession.Original.Count);

        string unit = dealsLeft == 1 ? "item" : "items";
        Game1.addHUDMessage(new HUDMessage($"Kiosk deal: {this.context.Config.KioskDiscountPercent}% off, up to {dealsLeft} {unit} today.", HUDMessage.achievement_type));
    }

    /// <summary>Records what the shop lists right now. Only plain, paid items get the discount (not items bought with a trade item, not free ones).</summary>
    private void Snapshot(DealSession s)
    {
        s.Original.Clear();
        s.LastStock.Clear();
        s.Known.Clear();

        foreach (ISalable item in s.Shop.itemPriceAndStock.Keys.ToList())
        {
            s.Known.Add(item);
            ItemStockInformation info = s.Shop.itemPriceAndStock[item];
            if (info.TradeItem != null || info.Price <= 0)
                continue;

            // 3.16.3: never treat a price we already discounted as the original (that compounded 10% off over and
            // over until every item cost 5 gold). The first full-price entry seen for an item id is the original.
            string id = item.QualifiedItemId;
            if (s.OriginalById.TryGetValue(id, out ItemStockInformation firstSeen))
            {
                info.Price = firstSeen.Price;
                info.Stock = firstSeen.Stock; // remaining stock is worked out from Bought by id
            }
            else
            {
                s.OriginalById[id] = info;
            }

            s.Original[item] = info;
        }
    }

    /// <summary>
    /// Keeps the lock in place: if the shop (the phone version in particular) puts a price or a bigger stock back
    /// on a discounted entry, set it to the discounted price and "deals left" again.
    /// </summary>
    private void Reassert(DealSession s)
    {
        int left = this.DealsLeft();
        if (left <= 0)
            return;

        foreach (KeyValuePair<ISalable, ItemStockInformation> entry in s.Original)
        {
            if (!s.Shop.itemPriceAndStock.TryGetValue(entry.Key, out ItemStockInformation info))
                continue;
            if (!s.LastStock.TryGetValue(entry.Key, out int expectedStock))
                continue;

            int expectedPrice = this.DiscountedPrice(entry.Value.Price);
            if (info.Price == expectedPrice && info.Stock <= expectedStock)
                continue;

            DevLog.Say("kiosk lock reasserted on " + entry.Key.Name + ": stock " + info.Stock + " -> " + expectedStock);
            info.Price = expectedPrice;
            info.Stock = expectedStock;
            s.Shop.itemPriceAndStock[entry.Key] = info;
        }
    }

    /// <summary>Called every tick by ModEntry. Counts what was bought and keeps the limits up to date.</summary>
    public void OnUpdateTicked()
    {
        DealSession? current = this.session;
        if (current == null)
            return;

        try
        {
            this.Poll(current);

            // Shop closed: one last count (done above), then forget it. A new visit builds a new shop.
            if (!ReferenceEquals(Game1.activeClickableMenu, current.Shop) && ReferenceEquals(this.session, current))
                this.session = null;
        }
        catch (Exception ex)
        {
            this.session = null;
            this.context.Monitor.Log($"Could not update kiosk deals: {ex}", LogLevel.Error);
        }
    }

    private void Poll(DealSession s)
    {
        // The shop listed entries we have never seen: it rebuilt its list (new objects). Start over from what it has now.
        foreach (ISalable key in s.Shop.itemPriceAndStock.Keys)
        {
            if (s.Known.Contains(key))
                continue;

            DevLog.Say("kiosk: the shop rebuilt its list, locking the stock again");
            this.Snapshot(s);
            int dealsNow = this.DealsLeft();
            if (dealsNow > 0)
                this.ApplyLimits(s, dealsNow);
            return;
        }

        // How many units of each entry were sold since the last look.
        List<KeyValuePair<ISalable, int>> sold = new List<KeyValuePair<ISalable, int>>();
        foreach (ISalable item in s.Original.Keys.ToList())
        {
            if (!s.LastStock.TryGetValue(item, out int last))
                continue;

            // An entry that sold out is removed from the shop by the game, which counts as stock 0.
            int now = s.Shop.itemPriceAndStock.TryGetValue(item, out ItemStockInformation info) ? info.Stock : 0;
            if (now >= last)
                continue;

            int units = last - now;
            s.LastStock[item] = now;
            string boughtId = item.QualifiedItemId;
            s.Bought[boughtId] = (s.Bought.TryGetValue(boughtId, out int before) ? before : 0) + units;
            sold.Add(new KeyValuePair<ISalable, int>(item, units));
        }

        if (sold.Count == 0)
        {
            this.Reassert(s);
            return;
        }

        int left = this.DealsLeft();
        int totalSold = 0;
        foreach (KeyValuePair<ISalable, int> entry in sold)
            totalSold += entry.Value;

        int discountedUnits = Math.Min(totalSold, left);
        DevLog.Say("kiosk purchase seen: units = ", totalSold);
        DevLog.Say("kiosk deals left before = ", left, left - discountedUnits);

        // Safety net: the shop charged every unit the discounted price. Units beyond the remaining deals
        // (should not happen any more because of the stock cap) pay the full price instead.
        int fullPriceUnits = totalSold - discountedUnits;
        if (fullPriceUnits > 0)
        {
            int toCharge = 0;
            int skip = discountedUnits;
            foreach (KeyValuePair<ISalable, int> entry in sold)
            {
                int units = entry.Value;
                int free = Math.Min(units, skip);
                skip -= free;
                int extra = units - free;
                if (extra > 0)
                {
                    int original = s.Original[entry.Key].Price;
                    toCharge += extra * (original - this.DiscountedPrice(original));
                }
            }

            if (toCharge > 0)
                Game1.player.Money = Math.Max(0, Game1.player.Money - toCharge);
        }

        this.SetDealsLeft(left - discountedUnits);
        int newLeft = this.DealsLeft();

        if (newLeft > 0)
        {
            this.ApplyLimits(s, newLeft);
            return;
        }

        this.FinishDeals(s);
    }

    /// <summary>Discounted price and "at most <paramref name="left"/> in stock" for every entry that is still in the shop.</summary>
    private void ApplyLimits(DealSession s, int left)
    {
        foreach (KeyValuePair<ISalable, ItemStockInformation> entry in s.Original)
        {
            ISalable item = entry.Key;
            if (!s.Shop.itemPriceAndStock.TryGetValue(item, out ItemStockInformation info))
                continue;

            int remaining = this.RemainingOriginalStock(s, item);
            info.Price = this.DiscountedPrice(entry.Value.Price);
            info.Stock = Math.Min(remaining, left);
            s.Shop.itemPriceAndStock[item] = info;
            s.LastStock[item] = info.Stock;
        }
    }

    /// <summary>The last deal was used: full prices and the shop's normal stock come back (entries the limit sold out are put back).</summary>
    private void FinishDeals(DealSession s)
    {
        bool addedBack = false;

        foreach (KeyValuePair<ISalable, ItemStockInformation> entry in s.Original)
        {
            ISalable item = entry.Key;
            int remaining = this.RemainingOriginalStock(s, item);
            if (remaining <= 0)
                continue; // really sold out in the shop's own stock, stays gone

            ItemStockInformation restored = entry.Value;
            restored.Stock = remaining;

            if (s.Shop.itemPriceAndStock.ContainsKey(item))
            {
                s.Shop.itemPriceAndStock[item] = restored;
            }
            else
            {
                s.Shop.forSale.Add(item);
                s.Shop.itemPriceAndStock[item] = restored;
                addedBack = true;
            }

            s.LastStock[item] = remaining;
        }

        if (addedBack)
            this.RefreshShopList(s.Shop);

        this.session = null;
        Game1.addHUDMessage(new HUDMessage("That was your last kiosk deal today. Regular prices from now on.", HUDMessage.achievement_type));
    }

    /// <summary>Asks the shop to rebuild its visible list after entries were added back. Optional: if the method is not there, the list updates the next time you scroll.</summary>
    private void RefreshShopList(ShopMenu shop)
    {
        try
        {
            this.context.Helper.Reflection.GetMethod(shop, "applyTab", false)?.Invoke();
        }
        catch (Exception ex)
        {
            DevLog.Say("kiosk shop list refresh skipped: " + ex.Message);
        }
    }

    /// <summary>The shop's own stock for an entry minus what was bought this visit. int.MaxValue = unlimited.</summary>
    private int RemainingOriginalStock(DealSession s, ISalable item)
    {
        int original = s.Original[item].Stock;
        if (original == int.MaxValue)
            return int.MaxValue;

        int bought = s.Bought.TryGetValue(item.QualifiedItemId, out int value) ? value : 0;
        return Math.Max(0, original - bought);
    }

    private int DiscountedPrice(int price)
    {
        double factor = 1.0 - this.context.Config.KioskDiscountPercent / 100.0;
        return Math.Max(1, (int)Math.Round(price * factor, MidpointRounding.AwayFromZero));
    }

    // ---- per-day counter (save format identical to the old DLL: "totalDays:unitsUsed") ----

    private int DealsLeft()
    {
        int perDay = this.context.Config.KioskDealsPerDay;
        string today = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);

        if (Game1.player.modData.TryGetValue(DiscountDataKey, out string? raw) && raw != null)
        {
            string[] parts = raw.Split(':');
            if (parts.Length == 2 && parts[0] == today && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int used))
                return Math.Max(0, perDay - used);
        }

        return perDay; // new day, or nothing stored yet
    }

    private void SetDealsLeft(int left)
    {
        int perDay = this.context.Config.KioskDealsPerDay;
        int used = perDay - Math.Max(0, Math.Min(perDay, left));
        string today = Game1.Date.TotalDays.ToString(CultureInfo.InvariantCulture);
        Game1.player.modData[DiscountDataKey] = today + ":" + used.ToString(CultureInfo.InvariantCulture);
    }
}
