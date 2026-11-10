using System;
using System.Collections.Generic;
using System.IO;
using BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.FastFood;

/// <summary>The restaurant front door and the staff (bedroom) door.</summary>
internal sealed class DoorController
{
    private sealed class DoorTile
    {
        public string DoorId { get; }
        public Point Tile { get; }
        public string TargetLocation { get; }
        public Point TargetTile { get; }
        public Func<GameLocation, bool> AppliesTo { get; }

        public DoorTile(string doorId, Point tile, string targetLocation, Point targetTile, Func<GameLocation, bool> appliesTo)
        {
            this.DoorId = doorId;
            this.Tile = tile;
            this.TargetLocation = targetLocation;
            this.TargetTile = targetTile;
            this.AppliesTo = appliesTo;
        }
    }

    private const string MainEntrance = "MainEntrance";
    private const string BedroomEntrance = "BedroomEntrance";

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly List<DoorTile> doorTiles = new();

    public DoorController(IModHelper helper, IMonitor monitor)
    {
        this.helper = helper;
        this.monitor = monitor;

        string restaurant = ModContext.RestaurantLocation;
        string bedroom = ModContext.BedroomLocation;

        this.doorTiles.Add(new DoorTile(MainEntrance, new Point(68, 25), restaurant, new Point(15, 12), IsForestMap));
        this.doorTiles.Add(new DoorTile(MainEntrance, new Point(69, 25), restaurant, new Point(15, 12), IsForestMap));
        this.doorTiles.Add(new DoorTile(BedroomEntrance, new Point(22, 3), bedroom, new Point(6, 9), loc => loc.Name == restaurant));
        this.doorTiles.Add(new DoorTile(BedroomEntrance, new Point(23, 3), bedroom, new Point(7, 9), loc => loc.Name == restaurant));
    }

    public void OnButtonPressed(ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || (!e.Button.IsActionButton() && !e.Button.IsUseToolButton()))
            return;

        Farmer player = Game1.player;
        GameLocation? location = player.currentLocation;
        if (location == null)
            return;

        Point cursorTile = e.Cursor.GrabTile.ToPoint();
        DoorTile? door = this.FindDoorTile(location, cursorTile);
        if (door == null)
            return;

        DevLog.Say("action button pressed near a door tile: ", door.DoorId);

        Point playerTile = player.TilePoint;
        if (Math.Abs(playerTile.X - cursorTile.X) > 2 || Math.Abs(playerTile.Y - cursorTile.Y) > 2)
        {
            DevLog.Say("door tile found but the player is more than 2 tiles away, ignored");
            return;
        }

        this.helper.Input.Suppress(e.Button);
        this.UseDoor(door, player);
    }

    private bool IsOpen(string doorId, Farmer who)
    {
        bool open;
        if (doorId == MainEntrance)
            open = DoorRules.IsRestaurantOpen();
        else if (doorId == BedroomEntrance)
        {
            int hearts = who.getFriendshipHeartLevelForNPC(ModContext.BentleyNpcName);
            DevLog.Say("hearts with Bentley (bedroom check) = ", hearts);
            open = DoorRules.IsBedroomOpen(hearts);
        }
        else
            open = false;

        DevLog.Say("IsOpen (door unlocked) = ", open);
        return open;
    }

    private void UseDoor(DoorTile door, Farmer who)
    {
        try
        {
            DevLog.Say("UseDoor: door id = ", door.DoorId);

            if (!this.IsOpen(door.DoorId, who))
            {
                string message = door.DoorId == MainEntrance ? DoorRules.RestaurantClosedMessage : DoorRules.BedroomLockedMessage;
                DevLog.Say("door is closed/locked, message shown: ", message);
                Game1.drawObjectDialogue(message);
            }
            else if (Game1.getLocationFromName(door.TargetLocation) == null)
            {
                this.monitor.Log($"Door '{door.DoorId}' could not open: location '{door.TargetLocation}' does not exist. Is the Content Patcher pack installed?", LogLevel.Error);
                Game1.drawObjectDialogue("The door will not open.");
            }
            else
            {
                this.monitor.Log($"Door '{door.DoorId}' open; warping to {door.TargetLocation} ({door.TargetTile.X}, {door.TargetTile.Y}).", LogLevel.Info);
                Game1.warpFarmer(door.TargetLocation, door.TargetTile.X, door.TargetTile.Y, who.FacingDirection);
            }
        }
        catch (Exception ex)
        {
            this.monitor.Log($"Door '{door.DoorId}' failed: {ex}", LogLevel.Error);
        }
    }

    private DoorTile? FindDoorTile(GameLocation location, Point tile)
    {
        foreach (DoorTile door in this.doorTiles)
        {
            if (door.Tile == tile && door.AppliesTo(location))
                return door;
        }
        return null;
    }

    private static bool IsForestMap(GameLocation location)
    {
        string? mapPath = location.mapPath.Value;
        if (string.IsNullOrEmpty(mapPath))
            return false;

        string fileName = Path.GetFileName(mapPath.Replace('\\', '/'));
        return fileName == "Forest" || fileName == "Forest-IceFestival" || fileName == "Forest-IceFestival2";
    }
}
