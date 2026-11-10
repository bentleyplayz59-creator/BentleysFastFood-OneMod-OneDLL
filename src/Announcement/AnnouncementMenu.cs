using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Announcement;

internal sealed class AnnouncementMenu : IClickableMenu
{
	private readonly struct DrawLine
	{
		public string Text { get; }

		public SpriteFont Font { get; }

		public Color Color { get; }

		public int Y { get; }

		public int Height { get; }

		public DrawLine(string text, SpriteFont font, Color color, int y, int height)
		{
			Text = text;
			Font = font;
			Color = color;
			Y = y;
			Height = height;
		}
	}

	private readonly string farmerName;

	private readonly Action onContinue;

	private readonly List<Texture2D> brandIcons = new List<Texture2D>();

	private readonly List<DrawLine> lines = new List<DrawLine>();

	private Rectangle textArea;

	private ClickableComponent continueButton = null!; // assigned in Layout(), which the constructor calls

	private ClickableTextureComponent upButton = null!; // assigned in Layout(), which the constructor calls

	private ClickableTextureComponent downButton = null!; // assigned in Layout(), which the constructor calls

	private int scrollY;

	private int maxScroll;

	private bool continueHover;

	private bool dragging;

	private int dragLastY;

	private bool dismissed;

	public AnnouncementMenu(string farmerName, Action onContinue)
		: base(0, 0, 0, 0, false)
	{
		this.farmerName = (string.IsNullOrWhiteSpace(farmerName) ? "Farmer" : farmerName);
		this.onContinue = onContinue;
		LoadBrandIcons();
		Layout();
		if (Game1.options.SnappyMenus)
		{
			((IClickableMenu)this).populateClickableComponentList();
			((IClickableMenu)this).snapToDefaultClickableComponent();
		}
	}

	public override bool readyToClose()
	{
		return false;
	}

	public override void snapToDefaultClickableComponent()
	{
		base.currentlySnappedComponent = continueButton;
		((IClickableMenu)this).snapCursorToCurrentSnappedComponent();
	}

	public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
	{
		base.gameWindowSizeChanged(oldBounds, newBounds);
		int val = scrollY;
		Layout();
		scrollY = Math.Min(val, maxScroll);
	}

	public override void receiveLeftClick(int x, int y, bool playSound = true)
	{
		if (continueButton.containsPoint(x, y))
		{
			Dismiss();
		}
		else if (upButton.containsPoint(x, y))
		{
			Scroll(-LineStep() * 3);
			Game1.playSound("shiny4", (int?)null);
		}
		else if (downButton.containsPoint(x, y))
		{
			Scroll(LineStep() * 3);
			Game1.playSound("shiny4", (int?)null);
		}
		else if (textArea.Contains(x, y))
		{
			dragging = true;
			dragLastY = y;
		}
	}

	public override void leftClickHeld(int x, int y)
	{
		if (dragging)
		{
			Scroll(dragLastY - y);
			dragLastY = y;
		}
	}

	public override void releaseLeftClick(int x, int y)
	{
		dragging = false;
	}

	public override void receiveRightClick(int x, int y, bool playSound = true)
	{
	}

	public override void receiveScrollWheelAction(int direction)
	{
		Scroll((direction > 0) ? (-LineStep() * 2) : (LineStep() * 2));
	}

	public override void performHoverAction(int x, int y)
	{
		continueHover = continueButton.containsPoint(x, y);
	}

	public override void receiveKeyPress(Keys key)
	{
		if (key == Keys.Enter || key == Keys.Space || Game1.options.doesInputListContain(Game1.options.actionButton, key))
		{
			Dismiss();
		}
		else if (key == Keys.Up || Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
		{
			Scroll(-LineStep());
		}
		else if (key == Keys.Down || Game1.options.doesInputListContain(Game1.options.moveDownButton, key))
		{
			Scroll(LineStep());
		}
		else if (key == Keys.PageUp)
		{
			Scroll(-textArea.Height);
		}
		else if (key == Keys.PageDown)
		{
			Scroll(textArea.Height);
		}
	}

	public override void receiveGamePadButton(Buttons b)
	{
		if (b == Buttons.A)
		{
			Dismiss();
		}
		else if (b == Buttons.DPadUp || b == Buttons.LeftThumbstickUp)
		{
			Scroll(-LineStep());
		}
		else if (b == Buttons.DPadDown || b == Buttons.LeftThumbstickDown)
		{
			Scroll(LineStep());
		}
	}

	public override void draw(SpriteBatch b)
	{
		b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.65f);
		IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), base.xPositionOnScreen, base.yPositionOnScreen, base.width, base.height, Color.White, 4f, true, -1f);
		DrawBranding(b);
		DrawText(b);
		DrawScrollControls(b);
		DrawContinue(b);
		((IClickableMenu)this).drawMouse(b, false, -1);
	}

	private void Layout()
	{
		int width = Game1.uiViewport.Width;
		int height = Game1.uiViewport.Height;
		base.width = Math.Min(width - 32, 960);
		base.height = Math.Min(height - 32, 720);
		base.xPositionOnScreen = (width - base.width) / 2;
		base.yPositionOnScreen = (height - base.height) / 2;
		int num = 136;
		int num2 = 104;
		int num3 = 56;
		int num4 = 48;
		textArea = new Rectangle(base.xPositionOnScreen + num3, base.yPositionOnScreen + num, base.width - num3 * 2 - num4, Math.Max(120, base.height - num - num2));
		continueButton = new ClickableComponent(new Rectangle(base.xPositionOnScreen + base.width / 2 - 130, base.yPositionOnScreen + base.height - num2 + 12, 260, 72), "Continue")
		{
			myID = 1000
		};
		upButton = new ClickableTextureComponent(new Rectangle(textArea.Right + 6, textArea.Y, 44, 48), Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f, false);
		downButton = new ClickableTextureComponent(new Rectangle(textArea.Right + 6, textArea.Bottom - 48, 44, 48), Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f, false);
		BuildLines();
	}

	private void BuildLines()
	{
		lines.Clear();
		int num = 0;
		foreach (AnnouncementContent.Block block in AnnouncementContent.GetBlocks(farmerName))
		{
			SpriteFont val = (block.Heading ? Game1.dialogueFont : Game1.smallFont);
			Color color = block.Heading ? new Color(92, 44, 12) : Game1.textColor;
			float num2 = val.MeasureString("Ag").Y + (float)(block.Heading ? 4 : 2);
			if (block.Heading && num > 0)
			{
				num += 10;
			}
			string text = Game1.parseText(block.Text, val, textArea.Width);
			string[] array = text.Split('\n');
			foreach (string text2 in array)
			{
				lines.Add(new DrawLine(text2, val, color, num, (int)num2));
				num += (int)num2;
			}
			num += (block.Heading ? 4 : 12);
		}
		maxScroll = Math.Max(0, num - textArea.Height);
		scrollY = Math.Min(scrollY, maxScroll);
	}

	private void LoadBrandIcons()
	{
		string[] array = new string[5] { "Burger", "Fries", "Coke", "Windex", "SnackPack" };
		foreach (string text in array)
		{
			try
			{
				brandIcons.Add(Game1.content.Load<Texture2D>("Mods/BentleyPlayz.FastFoodUsefulStuff/" + text));
			}
			catch
			{
			}
		}
	}

	private void DrawBranding(SpriteBatch b)
	{
		int num = base.xPositionOnScreen + base.width / 2;
		int num2 = base.yPositionOnScreen + 24;
		SpriteText.drawStringHorizontallyCenteredAt(b, "BENTLEY PLAYZ", num, num2, 999999, -1, 999999, 1f, 0.88f, false, (Color?)null, 99999);
		int num3 = 48;
		int count = brandIcons.Count;
		if (count <= 0)
		{
			return;
		}
		int num4 = count * num3 + (count - 1) * 8;
		int num5 = num - num4 / 2;
		int num6 = num2 + 48;
		foreach (Texture2D brandIcon in brandIcons)
		{
			b.Draw(brandIcon, new Vector2((float)num5, (float)num6), (Rectangle?)new Rectangle(0, 0, 16, 16), Color.White, 0f, Vector2.Zero, 3f, (SpriteEffects)0, 0.9f);
			num5 += num3 + 8;
		}
	}

	private void DrawText(SpriteBatch b)
	{
		foreach (DrawLine line in lines)
		{
			int num = line.Y - scrollY;
			if (num >= 0 && num + line.Height <= textArea.Height && line.Text.Length != 0)
			{
				Utility.drawTextWithShadow(b, line.Text, line.Font, new Vector2((float)textArea.X, (float)(textArea.Y + num)), line.Color, 1f, -1f, -1, -1, 1f, 3);
			}
		}
	}

	private void DrawScrollControls(SpriteBatch b)
	{
		if (maxScroll > 0)
		{
			upButton.draw(b, (scrollY > 0) ? Color.White : (Color.White * 0.4f), 0.9f, 0, 0, 0);
			downButton.draw(b, (scrollY < maxScroll) ? Color.White : (Color.White * 0.4f), 0.9f, 0, 0, 0);
			int num = upButton.bounds.Bottom + 6;
			int num2 = downButton.bounds.Y - 6;
			int num3 = Math.Max(10, num2 - num);
			int num4 = Math.Max(24, num3 * textArea.Height / (textArea.Height + maxScroll));
			int num5 = num + (num3 - num4) * scrollY / Math.Max(1, maxScroll);
			int num6 = upButton.bounds.X + upButton.bounds.Width / 2 - 4;
			b.Draw(Game1.staminaRect, new Rectangle(num6, num, 8, num3), new Color(120, 60, 20) * 0.25f);
			b.Draw(Game1.staminaRect, new Rectangle(num6, num5, 8, num4), new Color(120, 60, 20));
		}
	}

	private void DrawContinue(SpriteBatch b)
	{
		Rectangle bounds = continueButton.bounds;
		IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), bounds.X, bounds.Y, bounds.Width, bounds.Height, continueHover ? Color.Wheat : Color.White, 4f, false, -1f);
		Vector2 val = Game1.dialogueFont.MeasureString("Continue");
		Utility.drawTextWithShadow(b, "Continue", Game1.dialogueFont, new Vector2((float)bounds.Center.X - val.X / 2f, (float)bounds.Center.Y - val.Y / 2f + 2f), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
	}

	private int LineStep()
	{
		return (int)(Game1.smallFont.MeasureString("Ag").Y + 2f);
	}

	private void Scroll(int delta)
	{
		scrollY = Math.Max(0, Math.Min(maxScroll, scrollY + delta));
	}

	private void Dismiss()
	{
		if (!dismissed)
		{
			dismissed = true;
			Game1.playSound("bigDeSelect", (int?)null);
			((IClickableMenu)this).exitThisMenu(false);
			onContinue();
		}
	}
}
