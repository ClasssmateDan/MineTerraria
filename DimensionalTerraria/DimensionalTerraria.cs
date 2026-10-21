using Terraria;
using Terraria.ModLoader;
using Terraria.GameInput;
using System;
using System.Collections.Generic;

namespace DimensionalTerraria;

public sealed class DimensionalTerraria : Mod
{
    internal static ModKeybind ToggleDimension = null!;
    internal static ModKeybind ReturnToAnchor = null!;
    internal static ModKeybind OpenBackpack = null!;
    private static readonly string[] DefaultToggle = { "V" };
    private static readonly string[] DefaultReturn = { "B" };
    private static readonly string[] DefaultBackpack = { "E" };

    private static string BindingName(ModKeybind binding) => "DimensionalTerraria/" +
        (ReferenceEquals(binding, ToggleDimension) ? "ToggleDimension" : ReferenceEquals(binding, OpenBackpack) ? "OpenBackpack" : "ReturnToAnchor");

    internal static IReadOnlyList<string> AssignedKeys(ModKeybind binding)
    {
        // A fresh profile may draw its first world frame before tML reinitializes mod bindings.
        if (binding is not null && PlayerInput.CurrentProfile.InputModes.TryGetValue(InputMode.Keyboard, out var mode) &&
            mode.KeyStatus.TryGetValue(BindingName(binding), out var keys)) return keys;
        if (binding is null) return Array.Empty<string>();
        return ReferenceEquals(binding, ToggleDimension) ? DefaultToggle : ReferenceEquals(binding, OpenBackpack) ? DefaultBackpack : DefaultReturn;
    }

    internal static bool BindingCurrent(ModKeybind binding) => binding is not null &&
        PlayerInput.Triggers.Current.KeyStatus.TryGetValue(BindingName(binding), out bool held) && held;

    public override void Load()
    {
        Content.StorageMaterials.Register(this);
        if (Main.dedServ)
            return;
        RegisterBindings(this);
    }

    internal static void RegisterBindings(Mod mod)
    {
        ToggleDimension ??= KeybindLoader.RegisterKeybind(mod, "ToggleDimension", "V");
        ReturnToAnchor ??= KeybindLoader.RegisterKeybind(mod, "ReturnToAnchor", "B");
        OpenBackpack ??= KeybindLoader.RegisterKeybind(mod, "OpenBackpack", "E");
    }

    public override void Unload()
    {
        ToggleDimension = null!;
        ReturnToAnchor = null!;
        OpenBackpack = null!;
    }
}
