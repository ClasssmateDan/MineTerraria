using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace DimensionalTerraria.Configuration;

public sealed class DimensionConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [DefaultValue(true)]
    public bool EnableSwitching { get; set; } = true;

    [DefaultValue(true)]
    public bool EnableExplorationFog { get; set; } = true;

    [DefaultValue(true)]
    public bool ShowCreatures { get; set; } = true;

    [DefaultValue(true)]
    public bool Enable3DMining { get; set; } = true;

    [DefaultValue(true)]
    public bool Enable3DCombat { get; set; } = true;

    [DefaultValue(true)]
    public bool Enable3DPlacement { get; set; } = true;

    [DefaultValue(true)]
    public bool Enable3DPickup { get; set; } = true;

    [DefaultValue(6f), Range(2f, 12f), Increment(1f)]
    public float PickupRadius { get; set; } = 6f;

    [DefaultValue(40), Range(12, 100)]
    public int EnemyNoticeRange { get; set; } = 40;

    [DefaultValue(64), Range(20, 160)]
    public int EnemyLoseRange { get; set; } = 64;

    [DefaultValue(32f), Range(16f, 64f), Increment(4f)]
    public float EntryDistance { get; set; } = 32f;

    [Slider, DefaultValue(0.06f), Range(0.01f, 0.3f), Increment(0.01f)]
    public float MouseSensitivity { get; set; } = 0.06f;

    [DefaultValue(75), Range(60, 100)]
    public int FieldOfView { get; set; } = 75;

    [DefaultValue(4), Range(2, 6)]
    public int RenderDistance { get; set; } = 4;

    [DefaultValue(12f), Range(2f, 30f), Increment(1f)]
    public float FlightSpeed { get; set; } = 12f;

    [DefaultValue(true)]
    public bool SmoothEdges { get; set; } = true;

    [DefaultValue(false)]
    public bool ShowLandingOutline { get; set; }

}
