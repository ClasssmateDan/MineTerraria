using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DimensionalTerraria.Players;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class SteveCharacterCreation : ModSystem
{
    private readonly ConditionalWeakTable<UICharacterSelect, UITextPanel<LocalizedText>> buttons = new();
    private readonly List<WeakReference<UITextPanel<LocalizedText>>> attached = new();

    public override void Load() => On_UICharacterSelect.OnActivate += Activate;

    public override void Unload()
    {
        On_UICharacterSelect.OnActivate -= Activate;
        foreach (var reference in attached) if (reference.TryGetTarget(out var button)) button.Remove();
        attached.Clear();
        buttons.Clear();
    }

    private void Activate(On_UICharacterSelect.orig_OnActivate original, UICharacterSelect screen)
    {
        original(screen);
        if (buttons.TryGetValue(screen, out _)) return;
        UITextPanel<LocalizedText> button = new(Language.GetText("Mods.DimensionalTerraria.Character.NewSteve"), 0.8f);
        button.Width.Set(0, 0.8f);
        button.MaxWidth.Set(300, 0);
        button.Height.Set(34, 0);
        button.SetPadding(5);
        button.HAlign = 0.5f;
        button.VAlign = 1;
        button.Top.Set(-5, 0);
        button.BackgroundColor = new Color(30, 95, 105);
        button.OnMouseOver += (_, _) => { button.BackgroundColor = new Color(40, 135, 145); SoundEngine.PlaySound(SoundID.MenuTick); };
        button.OnMouseOut += (_, _) => button.BackgroundColor = new Color(30, 95, 105);
        button.OnLeftClick += NewSteve;
        screen.Append(button);
        buttons.Add(screen, button);
        attached.Add(new(button));
        screen.Recalculate();
    }

    private static void NewSteve(UIMouseEvent mouse, UIElement element)
    {
        SoundEngine.PlaySound(SoundID.MenuOpen);
        Player player = new() { name = "Steve" };
        player.GetModPlayer<SteveCharacter>().ChooseSteve();
        Main.PendingPlayer = player;
        Main.menuMode = 888;
        // Native creation handles the name, difficulty, starting inventory and
        // its own independent player file. Cancelling does not create a file.
        Main.MenuUI.SetState(new UICharacterCreation(player));
    }
}
