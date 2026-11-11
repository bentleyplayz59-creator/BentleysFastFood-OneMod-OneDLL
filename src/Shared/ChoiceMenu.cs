using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace BentleyPlayz.FastFoodUsefulStuff.Smapi.Shared;

/// <summary>
/// The "which shop?" picker used by the ordering counter and the Restaurant Kiosk.
/// It replaces the vanilla question dialogue because that box did not close on the back button
/// (mostly on mobile). This menu can always be left:
///   - the X button in the top right corner (tap / click),
///   - the Cancel button at the bottom,
///   - tapping or clicking outside the box,
///   - Esc / the game's menu key / Backspace, right click,
///   - B or Back on a controller,
///   - the phone's back button (handled in <see cref="TryHandleBackButton"/>, which ModEntry calls on every button press).
/// </summary>
internal sealed class ChoiceMenu : IClickableMenu
{
    /// <summary>The answer key that means "close without choosing anything".</summary>
    public const string CancelKey = "Esc";

    /// <summary>Ignore presses for this many ticks after opening, so the press that opened the menu cannot also pick an entry.</summary>
    private const int InputGraceTicks = 10;

    private readonly string title;
    private readonly List<KeyValuePair<string, string>> choices;
    private readonly Action<string> onChoice;
    private readonly List<ClickableComponent> buttons = new List<ClickableComponent>();
    private readonly int openedTick;

    private ClickableTextureComponent closeButton = null!; // assigned in Layout(), which the constructor calls
    private string[] titleLines = new string[0];
    private int titleLineHeight;
    private int selected;
    private bool finished;

    private ChoiceMenu(string title, IEnumerable<KeyValuePair<string, string>> choices, Action<string> onChoice)
        : base(0, 0, 0, 0, false)
    {
        this.title = title;
        this.choices = new List<KeyValuePair<string, string>>(choices);
        this.onChoice = onChoice;
        this.openedTick = Game1.ticks;
        Layout();

        // Controller / keyboard-only play: let the game's own cursor snapping work on our buttons.
        if (Game1.options.SnappyMenus)
        {
            ((IClickableMenu)this).snapToDefaultClickableComponent();
        }
    }

    public override void snapToDefaultClickableComponent()
    {
        if (buttons.Count == 0)
            return;

        if (selected < 0 || selected >= buttons.Count)
            selected = 0;

        base.currentlySnappedComponent = buttons[selected];
        ((IClickableMenu)this).snapCursorToCurrentSnappedComponent();
    }

    /// <summary>Opens the picker. <paramref name="onChoice"/> gets the key of the chosen entry; it is not called when the menu is cancelled.</summary>
    public static void Show(string title, IEnumerable<KeyValuePair<string, string>> choices, Action<string> onChoice)
    {
        Game1.activeClickableMenu = new ChoiceMenu(title, choices, onChoice);
    }

    /// <summary>
    /// Call this from the ButtonPressed event. If a ChoiceMenu is open and the back button (Esc, B, Back, phone back) was
    /// pressed, the menu is closed and the press is swallowed. Returns true when it did that.
    /// </summary>
    public static bool TryHandleBackButton(ButtonPressedEventArgs e, IModHelper helper)
    {
        if (!(Game1.activeClickableMenu is ChoiceMenu menu))
            return false;

        SButton button = e.Button;
        if (button == SButton.Escape || button == SButton.ControllerB || button == SButton.ControllerBack || button == SButton.BrowserBack)
        {
            helper.Input.Suppress(button);
            menu.Cancel();
            return true;
        }

        return false;
    }

    public override bool readyToClose()
    {
        return true;
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        Layout();
    }

    /// <summary>Closes the menu without choosing anything.</summary>
    public void Cancel()
    {
        if (finished)
            return;

        finished = true;
        Game1.playSound("bigDeSelect", (int?)null);
        ((IClickableMenu)this).exitThisMenu(false);
    }

    private void Choose(int index)
    {
        if (finished || index < 0 || index >= choices.Count)
            return;

        string key = choices[index].Key;
        if (key == CancelKey)
        {
            Cancel();
            return;
        }

        finished = true;
        Game1.playSound("smallSelect", (int?)null);

        // Close this menu first: the callback usually opens a shop, and that must not be closed by our exit.
        ((IClickableMenu)this).exitThisMenu(false);
        try
        {
            onChoice(key);
        }
        catch (Exception ex)
        {
            DevLog.Say("choice menu callback failed: " + ex.Message);
            if (DevLog.Monitor != null)
                DevLog.Monitor.Log($"Choice menu callback failed: {ex}", LogLevel.Error);
        }
    }

    private bool InGracePeriod()
    {
        return Game1.ticks - openedTick < InputGraceTicks;
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (finished || InGracePeriod())
            return;

        if (closeButton.containsPoint(x, y))
        {
            Cancel();
            return;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].containsPoint(x, y))
            {
                Choose(i);
                return;
            }
        }

        // A tap or click outside the box closes it (this is the touch-screen "back").
        Rectangle box = new Rectangle(base.xPositionOnScreen, base.yPositionOnScreen, base.width, base.height);
        if (!box.Contains(x, y))
            Cancel();
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        if (finished || InGracePeriod())
            return;

        Cancel();
    }

    public override void performHoverAction(int x, int y)
    {
        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i].containsPoint(x, y))
            {
                selected = i;
                return;
            }
        }
    }

    public override void receiveKeyPress(Keys key)
    {
        if (finished)
            return;

        if (key == Keys.Escape || key == Keys.Back || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            Cancel();
        }
        else if (key == Keys.Up || Game1.options.doesInputListContain(Game1.options.moveUpButton, key))
        {
            Move(-1);
        }
        else if (key == Keys.Down || Game1.options.doesInputListContain(Game1.options.moveDownButton, key))
        {
            Move(1);
        }
        else if (!InGracePeriod() && (key == Keys.Enter || key == Keys.Space || Game1.options.doesInputListContain(Game1.options.actionButton, key)))
        {
            Choose(selected);
        }
    }

    public override void receiveGamePadButton(Buttons b)
    {
        if (finished)
            return;

        if (b == Buttons.B || b == Buttons.Back)
        {
            Cancel();
        }
        else if (b == Buttons.DPadUp || b == Buttons.LeftThumbstickUp)
        {
            Move(-1);
        }
        else if (b == Buttons.DPadDown || b == Buttons.LeftThumbstickDown)
        {
            Move(1);
        }
        else if (b == Buttons.A && !InGracePeriod())
        {
            Choose(selected);
        }
    }

    private void Move(int delta)
    {
        if (buttons.Count == 0)
            return;

        selected = (selected + delta + buttons.Count) % buttons.Count;
        Game1.playSound("shiny4", (int?)null);

        if (Game1.options.SnappyMenus)
        {
            base.currentlySnappedComponent = buttons[selected];
            ((IClickableMenu)this).snapCursorToCurrentSnappedComponent();
        }
    }

    public override void draw(SpriteBatch b)
    {
        // Same look as the game's own question box: the dialogue frame, left-aligned text, pale highlight on the chosen line.
        // Dim the map behind the box so the title is readable, then draw the frame at exactly our own coordinates
        // (drawDialogueBox placed the frame lower than the title on some devices, leaving the title floating over the map).
        b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
        IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60), base.xPositionOnScreen, base.yPositionOnScreen, base.width, base.height, Color.White, 1f, true, -1f);

        int textY = base.yPositionOnScreen + 28;
        foreach (string line in titleLines)
        {
            Utility.drawTextWithShadow(b, line, Game1.dialogueFont, new Vector2((float)(base.xPositionOnScreen + 32), (float)textY), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
            textY += titleLineHeight;
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            Rectangle bounds = buttons[i].bounds;
            if (i == selected)
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3), bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White, 4f, false, -1f);

            string label = choices[i].Value;
            Vector2 size = Game1.dialogueFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.dialogueFont, new Vector2((float)(bounds.X + 16), (float)bounds.Center.Y - size.Y / 2f + 2f), Game1.textColor, 1f, -1f, -1, -1, 1f, 3);
        }

        closeButton.draw(b, Color.White, 0.9f, 0, 0, 0);
        ((IClickableMenu)this).drawMouse(b, false, -1);
    }

    private void Layout()
    {
        int viewWidth = Game1.uiViewport.Width;
        int viewHeight = Game1.uiViewport.Height;

        titleLineHeight = (int)Game1.dialogueFont.MeasureString("Ag").Y;
        int rowHeight = titleLineHeight + 16;
        int gap = 4;

        // Width: wide enough for the longest line, never wider than the screen.
        float widest = Game1.dialogueFont.MeasureString(title).X;
        foreach (KeyValuePair<string, string> choice in choices)
            widest = Math.Max(widest, Game1.dialogueFont.MeasureString(choice.Value).X);
        base.width = Math.Min(viewWidth - 32, Math.Max(480, (int)widest + 140));

        string wrapped = Game1.parseText(title, Game1.dialogueFont, base.width - 130);
        titleLines = wrapped.Split('\n');

        int wanted = 28 + titleLines.Length * titleLineHeight + 16 + choices.Count * (rowHeight + gap) + 24;
        base.height = Math.Min(wanted, viewHeight - 32);

        // Squeeze the rows if the screen is too short (phone in landscape).
        if (wanted > base.height && choices.Count > 0)
        {
            int available = base.height - (28 + titleLines.Length * titleLineHeight + 16 + 24);
            rowHeight = Math.Max(40, available / choices.Count - gap);
        }

        // Like the vanilla question box: centered, near the bottom of the screen.
        base.xPositionOnScreen = (viewWidth - base.width) / 2;
        base.yPositionOnScreen = Math.Max(16, viewHeight - base.height - 48);

        buttons.Clear();
        int y = base.yPositionOnScreen + 28 + titleLines.Length * titleLineHeight + 16;
        for (int i = 0; i < choices.Count; i++)
        {
            ClickableComponent button = new ClickableComponent(new Rectangle(base.xPositionOnScreen + 28, y, base.width - 56, rowHeight), choices[i].Key)
            {
                myID = 1000 + i,
                upNeighborID = i > 0 ? 1000 + i - 1 : -1,
                downNeighborID = i < choices.Count - 1 ? 1000 + i + 1 : -1
            };
            buttons.Add(button);
            y += rowHeight + gap;
        }

        base.allClickableComponents = new List<ClickableComponent>(buttons);

        if (selected >= buttons.Count)
            selected = 0;

        // X in the top right corner, inside the frame.
        closeButton = new ClickableTextureComponent(new Rectangle(base.xPositionOnScreen + base.width - 64, base.yPositionOnScreen + 14, 40, 40), Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 3.3f, false);
    }
}
