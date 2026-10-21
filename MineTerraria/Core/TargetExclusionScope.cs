using System;

namespace DimensionalTerraria.Core;

public readonly record struct TargetPresence(bool Active, bool Dead, bool Ghost);

public interface ITargetablePlayer
{
    TargetPresence Presence { get; set; }
}

public interface IAggroMemory
{
    int Target { get; set; }
    int PreviousTarget { get; set; }
    void ClearTargetArea();
}

// Exclude the spectator only during AI/target searches. The outer world tick
// still keeps its presence for spawning and NPC activity checks.
public sealed class TargetExclusionScope : IDisposable
{
    public const int NoPlayer = 255;
    private ITargetablePlayer? player;
    private readonly TargetPresence saved;
    private readonly IAggroMemory? memory;
    private readonly int excludedPlayer;

    public TargetExclusionScope(ITargetablePlayer player, int excludedPlayer, IAggroMemory? memory = null)
    {
        this.player = player;
        this.memory = memory;
        this.excludedPlayer = excludedPlayer;
        saved = player.Presence;
        player.Presence = new TargetPresence(false, true, true);
        if (memory is not null) ForgetPlayer(memory, excludedPlayer);
    }

    public static void ForgetPlayer(IAggroMemory memory, int excludedPlayer)
    {
        if (memory.Target == excludedPlayer || memory.Target == NoPlayer && memory.PreviousTarget == excludedPlayer)
        {
            memory.Target = NoPlayer;
            memory.ClearTargetArea();
        }
        if (memory.PreviousTarget == excludedPlayer) memory.PreviousTarget = NoPlayer;
    }

    public static void SerializeAlive(ITargetablePlayer player, Action serialize)
        => SerializePresence(player, new TargetPresence(true, false, false), serialize);

    public static void SerializePresence(ITargetablePlayer player, TargetPresence realPresence, Action serialize)
    {
        // Player files persist the dead flag. A save nested in AI (or a queued
        // background autosave) must see the real, living 2D character state.
        TargetPresence before = player.Presence;
        player.Presence = realPresence;
        try { serialize(); }
        finally { player.Presence = before; }
    }

    public void Dispose()
    {
        if (player is null) return;
        try
        {
            if (memory is not null) ForgetPlayer(memory, excludedPlayer);
        }
        finally
        {
            player.Presence = saved;
            player = null;
        }
    }
}
