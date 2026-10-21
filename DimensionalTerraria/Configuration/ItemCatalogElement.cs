using System;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader.Config.UI;

namespace DimensionalTerraria.Configuration;

public class ItemCatalogElement : ConfigElement
{
    private const int Columns = 6, Rows = ItemCatalog.PageSize / Columns, Cell = 52, GridTop = 137;
    private const string Prefix = "Mods.DimensionalTerraria.Catalog.";
    private ItemCatalog catalog = new(Array.Empty<CatalogEntry>());
    private UISearchBar? search;
    private string? result;
    protected virtual bool Vanilla => false;

    public override void OnBind()
    {
        base.OnBind();
        catalog = new(CatalogItems.Entries(Vanilla));
        DrawLabel = false;
        Height.Set(GridTop + Rows * Cell + 31, 0);
        SetPadding(0);

        search = new UISearchBar(Language.GetText(Prefix + "SearchHint"), 0.65f);
        search.Left.Set(16, 0); search.Top.Set(60, 0);
        search.Width.Set(-104, 1); search.Height.Set(30, 0);
        search.OnContentsChanged += query => { catalog.Search(query); result = null; };
        search.OnLeftMouseDown += (_, _) => { if (!search.IsWritingText) search.ToggleTakingText(); };
        search.SetContents("");
        Append(search);

        var clear = Button("Clear", -66, 1, 58, 59);
        clear.OnLeftClick += (_, _) => { search.SetContents(""); StopTyping(); };
        var previous = Button("Previous", 8, 0, 76, 100);
        previous.OnLeftClick += (_, _) => catalog.MovePage(-1);
        var next = Button("Next", -84, 1, 76, 100);
        next.OnLeftClick += (_, _) => catalog.MovePage(1);

        OnLeftClick += (mouse, _) =>
        {
            if (!catalog.TryVisible(Hit(mouse.MousePosition), out CatalogEntry entry)) return;
            bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
            result = DimensionSystem.CatalogAvailable ? DimensionSystem.Instance.GiveCatalogItem(entry.Type, shift ? 64 : 1, Vanilla) :
                Language.GetTextValue(Prefix + "InGameOnly");
        };
    }

    private UITextPanel<string> Button(string key, float left, float fraction, float width, float top)
    {
        var button = new UITextPanel<string>(Language.GetTextValue(Prefix + key), 0.65f);
        button.SetPadding(4);
        button.Left.Set(left, fraction); button.Top.Set(top, 0);
        button.Width.Set(width, 0); button.Height.Set(28, 0);
        Append(button);
        return button;
    }

    private void StopTyping()
    {
        if (search is { IsWritingText: true }) search.ToggleTakingText();
    }

    public override void OnActivate() { base.OnActivate(); StopTyping(); }
    public override void OnDeactivate() { StopTyping(); base.OnDeactivate(); }

    public override void Update(GameTime gameTime)
    {
        Vector2 mouse = MousePosition();
        if (Main.mouseLeft && Main.mouseLeftRelease && search is { IsWritingText: true } && !search.ContainsPoint(mouse)) StopTyping();
        base.Update(gameTime);
    }

    private Rectangle Grid()
    {
        var area = GetDimensions();
        return new((int)(area.X + (area.Width - Columns * Cell) / 2), (int)area.Y + GridTop, Columns * Cell, Rows * Cell);
    }

    private static Vector2 MousePosition()
    {
        MouseState mouse = Mouse.GetState();
        return new(mouse.X / Main.UIScale, mouse.Y / Main.UIScale);
    }

    private int Hit(Vector2 point)
    {
        Rectangle grid = Grid();
        if (!grid.Contains(point.ToPoint())) return -1;
        return ((int)point.Y - grid.Y) / Cell * Columns + ((int)point.X - grid.X) / Cell;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch)
    {
        base.DrawSelf(spriteBatch);
        var area = GetDimensions();
        Utils.DrawBorderString(spriteBatch, Language.GetTextValue(Prefix + "Hint"), new(area.X + 8, area.Y + 8), Color.White, 0.65f);
        Utils.DrawBorderString(spriteBatch, result ?? Language.GetTextValue(Prefix + "Ready"), new(area.X + 8, area.Y + 31), Color.LightCyan, 0.55f);
        spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)area.X + 8, (int)area.Y + 58, (int)area.Width - 84, 32), new Color(30, 38, 60));
        string status = Language.GetTextValue(Prefix + "Page", catalog.Count, catalog.Page + 1, catalog.Pages);
        Utils.DrawBorderString(spriteBatch, status, new(area.X + area.Width / 2, area.Y + 106), Color.White, 0.6f, 0.5f);
        Rectangle grid = Grid();
        Vector2 mouse = MousePosition();
        int hover = Hit(mouse);
        for (int i = 0; i < catalog.VisibleCount; i++)
        {
            catalog.TryVisible(i, out CatalogEntry entry);
            Rectangle cell = new(grid.X + i % Columns * Cell + 2, grid.Y + i / Columns * Cell + 2, Cell - 4, Cell - 4);
            spriteBatch.Draw(TextureAssets.MagicPixel.Value, cell, i == hover ? new Color(105, 125, 165) : new Color(43, 52, 76));
            Main.instance.LoadItem(entry.Type);
            Texture2D texture = TextureAssets.Item[entry.Type].Value;
            Rectangle frame = Main.itemAnimations[entry.Type]?.GetFrame(texture) ?? texture.Bounds;
            spriteBatch.Draw(texture, cell.Center.ToVector2(), frame, DimensionSystem.CatalogAvailable ? Color.White : Color.Gray,
                0, frame.Size() / 2, Math.Min(1, 34f / Math.Max(frame.Width, frame.Height)), SpriteEffects.None, 0);
        }
        if (catalog.TryVisible(hover, out CatalogEntry hovered))
            Utils.DrawBorderString(spriteBatch, hovered.Name,
                new(grid.X, grid.Bottom + 5), Color.LightYellow, 0.65f);
        else if (catalog.Count == 0)
            Utils.DrawBorderString(spriteBatch, Language.GetTextValue(Prefix + "NoResults"),
                new(grid.Center.X, grid.Y + 20), Color.LightGray, 0.75f, 0.5f);
    }
}

public sealed class VanillaItemCatalogElement : ItemCatalogElement
{
    protected override bool Vanilla => true;
}
