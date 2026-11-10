using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Announcement;

internal static class AnnouncementContent
{
	internal readonly struct Block
	{
		public bool Heading { get; }

		public string Text { get; }

		public Block(bool heading, string text)
		{
			Heading = heading;
			Text = text;
		}
	}

	internal sealed class AnnouncementFile
	{
		public List<BlockEntry> Blocks { get; set; } = new List<BlockEntry>();
	}

	internal sealed class BlockEntry
	{
		public string Type { get; set; } = "text";

		public string Text { get; set; } = "";
	}

	public const string FileName = "announcement.json";

	public const string FarmerNameToken = "{farmerName}";

	private static List<Block> blocks = new List<Block>();

	public static string Version { get; private set; } = "";

	public static void Load(IModHelper helper, IMonitor monitor, string version)
	{
		AnnouncementFile? announcementFile = null;
		try
		{
			announcementFile = helper.Data.ReadJsonFile<AnnouncementFile>("announcement.json");
		}
		catch (Exception ex)
		{
			monitor.Log($"Could not read {"announcement.json"} ({ex.Message}). Using the built-in announcement.", (LogLevel)3);
		}
		if (announcementFile == null)
		{
			monitor.Log("announcement.json was not found next to the DLL. Using the built-in announcement.", (LogLevel)3);
			announcementFile = BuiltIn();
		}
		else if (announcementFile.Blocks == null || !announcementFile.Blocks.Any((BlockEntry b) => b != null && !string.IsNullOrWhiteSpace(b.Text)))
		{
			monitor.Log("announcement.json needs at least one block with text. Using the built-in announcement.", (LogLevel)3);
			announcementFile = BuiltIn();
		}
		Version = version.Trim();
		blocks = (from b in announcementFile.Blocks
			where b != null && !string.IsNullOrWhiteSpace(b.Text)
			select new Block(string.Equals(b.Type?.Trim(), "heading", StringComparison.OrdinalIgnoreCase), b.Text)).ToList();
		monitor.Log($"Announcement {Version} loaded ({blocks.Count} blocks).", (LogLevel)2);
	}

	public static IEnumerable<Block> GetBlocks(string farmerName)
	{
		foreach (Block block in blocks)
		{
			yield return new Block(block.Heading, block.Text.Replace("{farmerName}", farmerName));
		}
	}

	private static AnnouncementFile BuiltIn()
	{
		return new AnnouncementFile
		{
			Blocks = new List<BlockEntry>
			{
				new BlockEntry
				{
					Type = "heading",
					Text = "Bentley Playz Fast Food - New Update Available!"
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Welcome, {farmerName}!"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "Thank you for downloading the latest version of Bentley Playz Fast Food & Useful Stuff!"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "This update opens the restaurant around the clock, removes the Town Pass, and fixes the bedroom door collision."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "What's new in this update?"
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Open 24 Hours, Every Day"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "Bentley's Fast Food is now open all day and all night. The front door and the ordering counter never close."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Town Pass Removed"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "The Town Pass is gone because it is no longer needed. Any leftover pass is removed from your inventory."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Bedroom Door Collision Fixed"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "The staff door stays solid until you reach three hearts with Bentley, and is walkable once it opens."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Bedroom Access"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "Bedroom access continues to follow the existing progression requirement: you need three hearts with Bentley before the staff door opens."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Updated Shop Hours"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "The ordering counter is open 24/7."
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Special Thanks"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "A special thank-you to our friends for their support throughout this project's development."
				},
				new BlockEntry
				{
					Type = "text",
					Text = "We would also like to thank everyone in the Stardew Valley community who has downloaded the mod, shared feedback, and supported its continued development."
				},
				new BlockEntry
				{
					Type = "text",
					Text = "We genuinely appreciate your support."
				},
				new BlockEntry
				{
					Type = "text",
					Text = "Thank you for being part of Bentley Playz Fast Food & Useful Stuff!"
				},
				new BlockEntry
				{
					Type = "heading",
					Text = "Enjoy the update!"
				},
				new BlockEntry
				{
					Type = "text",
					Text = "Creator: Bentley Playz"
				}
			}
		};
	}
}
