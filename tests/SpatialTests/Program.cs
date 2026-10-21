using System.Numerics;
using DimensionalTerraria.Core;

int passed = 0;
Run("A held toggle fires only once", () => {
    var input = new ButtonEdge();
    Check(!input.Sample(false)); Check(input.Sample(true));
    for (int i = 0; i < 120; i++) Check(!input.Sample(true));
});
Run("Releasing a toggle permits the next press", () => {
    var input = new ButtonEdge(); Check(input.Sample(true));
    Check(!input.Sample(false)); Check(input.Sample(true));
});
Run("Empty space accepts a full-size player", () => Check(Validate(new TestWorld()) == ReturnFailure.None));
Run("Foreground blocks prevent returning", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Solid);
    Check(Validate(world) == ReturnFailure.Obstructed);
});
Run("A wall is not a 2D foreground obstruction", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Empty, HasWall: true);
    Check(Validate(world) == ReturnFailure.None);
});
Run("Checks the head and feet, not only the center tile", () => {
    var world = new TestWorld(); world[30, 29] = new(BlockShape.Solid);
    Check(Validate(world) == ReturnFailure.Obstructed);
});
Run("Checks the full player width", () => {
    var world = new TestWorld(); world[29, 30] = new(BlockShape.Solid);
    Check(Validate(world) == ReturnFailure.Obstructed);
});
Run("Touching the floor is allowed", () => {
    var world = new TestWorld(); world[30, 32] = new(BlockShape.Solid);
    Check(SpatialRules.ValidateReturn(world, new(30.5f, 30.5f), new(1, 3)) == ReturnFailure.None);
});
Run("Half blocks leave their upper half empty", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.HalfBlock);
    Check(SpatialRules.ValidateReturn(world, new(30.5f, 30.2f), new(0.2f, 0.2f)) == ReturnFailure.None);
    Check(SpatialRules.ValidateReturn(world, new(30.5f, 30.7f), new(0.2f, 0.2f)) == ReturnFailure.Obstructed);
});
Run("A platform cannot intersect the returning body", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Platform);
    Check(Validate(world) == ReturnFailure.Obstructed);
});
Run("Liquid positions are rejected", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Empty, HasLiquid: true);
    Check(Validate(world) == ReturnFailure.Liquid);
});
Run("World border includes the complete body", () => {
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(10, 30), new(1, 2)) == ReturnFailure.OutOfWorld);
});
Run("NaN coordinates are rejected without indexing the map", () => {
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(float.NaN, 30), new(1, 2)) == ReturnFailure.OutOfWorld);
});
Run("The wall is collidable in 3D", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Empty, HasWall: true);
    Check(!SpatialRules.CanOccupyFlight(world, new(30.5f, 30.5f, -1), new(1, 2)));
    Check(SpatialRules.CanOccupyFlight(world, new(30.5f, 30.5f, 2), new(1, 2)));
});
Run("Boost cannot tunnel through a thin foreground layer", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Solid);
    Vector3 moved = SpatialRules.Move(world, new(30.5f, 30.5f, 5), new(0, 0, -12), new(1, 2));
    Check(moved.Z >= 0.799f);
});
Run("Flight rejects the back of the sheet even without terrain", () => {
    var world = new TestWorld();
    Check(!SpatialRules.CanOccupyFlight(world, new(30, 30, -5), new(1, 2)));
    Check(!SpatialRules.CanOccupyFlight(world, new(30, 30, 0), new(1, 2)));
});
Run("Flight slides along blocked axes", () => {
    var world = new TestWorld();
    Vector3 moved = SpatialRules.Move(world, new(30, 30.5f, 1.2f), new(2, 0, -2), new(1, 1));
    Check(moved.X > 31.9f && moved.Z >= SpatialRules.MinFlightDepth - 0.001f && SpatialRules.CanOccupyFlight(world, moved, new(1, 1)));
});
Run("The air wall also stops boosted flight through an empty hole", () => {
    var world = new TestWorld();
    Vector3 moved = SpatialRules.Move(world, new(30, 30, 5), new(0, 0, -10), new(1, 2));
    Check(moved.Z >= SpatialRules.MinFlightDepth - 0.001f && moved.Z < 1);
});
Run("Departure animation reaches open space", () => {
    var flight = new FlightState(new(30, 30), new(1, 2));
    Check(flight.Position.Z >= 24);
    for (int i = 0; i < 30; i++) flight.Update(new TestWorld(), 1f / 60, Vector3.Zero, false);
    Check(!flight.Departing && Math.Abs(flight.Position.Z - 32) < 0.001f && flight.Fade == 1);
});
Run("Pitch clamps before the view becomes singular", () => {
    var flight = new FlightState(new(30, 30), new(1, 2)); flight.Look(100, 100);
    Check(flight.Pitch < MathF.PI / 2 && Math.Abs(flight.Forward.Length() - 1) < 0.0001f);
});
Run("W flies along the complete facing direction including pitch", () => {
    var world = new TestWorld(); var flight = new FlightState(new(30, 30), new(1, 2));
    for (int i = 0; i < 30; i++) flight.Update(world, 1f / 60, Vector3.Zero, false);
    flight.Look(0.7f, 1.2f); Vector3 start = flight.Position;
    flight.Update(world, 0.05f, Vector3.UnitZ, false);
    Vector3 movement = flight.Position - start;
    Check(Vector3.Dot(Vector3.Normalize(movement), flight.Forward) > 0.9999f);
    Check(movement.Y < 0 && movement.Z < 0 && movement.X > 0);
});
Run("Vertical flight input works independently of camera pitch", () => {
    var world = new TestWorld(); var flight = new FlightState(new(30, 30), new(1, 2));
    for (int i = 0; i < 30; i++) flight.Update(world, 1f / 60, Vector3.Zero, false);
    flight.Look(1, -1); var before = flight.Position;
    flight.Update(world, 0.05f, -Vector3.UnitY, false);
    Check(flight.Position.Y < before.Y && flight.Position.X == before.X && flight.Position.Z == before.Z);
});
Run("Hotbar wheel wraps from first slot to last", () => Check(HotbarSelection.Scroll(0, 1) == 9));
Run("Hotbar wheel wraps from last slot to first", () => Check(HotbarSelection.Scroll(9, -1) == 0));
Run("Fast wheel scrolling remains in the actual ten-slot hotbar", () => {
    Check(HotbarSelection.Scroll(3, 27) == 6 && HotbarSelection.Scroll(3, -27) == 0);
});
Run("Diagonal creative flight is not faster than a single axis", () => {
    var world = new TestWorld(); var a = new FlightState(new(30, 30), new(1, 2)); var b = new FlightState(new(30, 30), new(1, 2));
    for (int i = 0; i < 30; i++) { a.Update(world, 1f / 60, Vector3.Zero, false); b.Update(world, 1f / 60, Vector3.Zero, false); }
    Vector3 start = a.Position; a.Update(world, 0.05f, Vector3.UnitX, false); b.Update(world, 0.05f, new(1, 0, 1), false);
    Check(Math.Abs(Vector3.Distance(start, a.Position) - Vector3.Distance(start, b.Position)) < 0.001f);
});
Run("Boost cannot leave either horizontal world boundary", () => {
    var world = new TestWorld();
    Vector3 left = SpatialRules.Move(world, new(12, 30, 20), new(-15, 0, 0), new(1, 2));
    Vector3 right = SpatialRules.Move(world, new(88, 30, 20), new(15, 0, 0), new(1, 2));
    Check(left.X >= 10.5f - 0.001f && right.X <= 89.5f + 0.001f);
});
Run("Boost cannot leave the sky or lower world boundary", () => {
    var world = new TestWorld();
    Vector3 top = SpatialRules.Move(world, new(30, 12, 20), new(0, -15, 0), new(1, 2));
    Vector3 bottom = SpatialRules.Move(world, new(30, 88, 20), new(0, 15, 0), new(1, 2));
    Check(top.Y >= 11 - 0.001f && bottom.Y <= 89 + 0.001f);
});
Run("Boost cannot pass the far depth boundary", () => {
    Vector3 moved = SpatialRules.Move(new TestWorld(), new(30, 30, 155), new(0, 0, 20), new(1, 2));
    Check(moved.Z + SpatialRules.BodyHalfDepth <= SpatialRules.MaxFlightDepth + 0.001f);
});
Run("Configured entry distance is reached without a near-wall first frame", () => {
    var flight = new FlightState(new(30, 30), new(1, 2), 64);
    Check(flight.Position.Z >= 48);
    for (int i = 0; i < 30; i++) flight.Update(new TestWorld(), 1f / 60, Vector3.Zero, false);
    Check(Math.Abs(flight.Position.Z - 64) < 0.001f);
});
Run("An invalid entry distance falls back to a finite position", () => {
    var flight = new FlightState(new(30, 30), new(1, 2), float.NaN);
    Check(float.IsFinite(flight.Position.Z) && flight.Position.Z == 24);
});
Run("UI scale 150 percent keeps full physical viewport coverage", () => {
    var layout = new ViewLayout(1920, 1080, 1.5f);
    Check(layout.UiWidth == 1280 && layout.UiHeight == 720);
    Check(layout.UiWidth * layout.UiScale == 1920 && layout.UiHeight * layout.UiScale == 1080);
    Check(layout.ToUi(layout.PixelCenter) == new Vector2(640, 360));
});
Run("A resized high-scale viewport keeps mouse and HUD centers aligned", () => {
    var layout = new ViewLayout(2560, 1440, 2);
    Check(layout.PixelCenter == new Vector2(1280, 720));
    Check(layout.ToUi(layout.PixelCenter) == new Vector2(layout.UiWidth / 2, layout.UiHeight / 2));
});
Run("Fog rejects an unexplored empty return position", () => {
    var mask = new ExplorationMask(100, 100, (_, _) => false);
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(30.5f, 30.5f), new(1, 2), exploration: mask) == ReturnFailure.Unexplored);
});
Run("Fog permits a fully explored return footprint", () => {
    var mask = new ExplorationMask(100, 100, (x, y) => x >= 29 && x <= 31 && y >= 29 && y <= 31);
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(30.5f, 30.5f), new(1.25f, 2.625f), exploration: mask) == ReturnFailure.None);
});
Run("Fog checks the player's head and full width", () => {
    var head = new ExplorationMask(100, 100, (_, y) => y != 29);
    var side = new ExplorationMask(100, 100, (x, _) => x != 29);
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(30.5f, 30.5f), new(1.25f, 2.625f), exploration: head) == ReturnFailure.Unexplored);
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(30.5f, 30.5f), new(1.25f, 2.625f), exploration: side) == ReturnFailure.Unexplored);
});
Run("A fog hint does not disclose whether hidden cells contain blocks", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Solid);
    var mask = new ExplorationMask(100, 100, (_, _) => false);
    Check(SpatialRules.ValidateReturn(world, new(30.5f, 30.5f), new(1, 2), exploration: mask) == ReturnFailure.Unexplored);
});
Run("Disabling fog removes the exploration restriction", () => {
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(30.5f, 30.5f), new(1, 2), exploration: null) == ReturnFailure.None);
});
Run("Exploration is fixed for a flight and refreshed on the next entry", () => {
    bool revealed = false;
    var first = new ExplorationMask(100, 100, (_, _) => revealed);
    revealed = true;
    var next = new ExplorationMask(100, 100, (_, _) => revealed);
    Check(!first.IsExplored(30, 30) && next.IsExplored(30, 30));
});
Run("Creature-sized exploration checks reject partially hidden sprites", () => {
    var mask = new ExplorationMask(100, 100, (x, _) => x < 32);
    Check(ExplorationMask.Covers(mask, new(30, 30), new(2, 2)));
    Check(!ExplorationMask.Covers(mask, new(31, 30), new(4, 2)));
});
Run("Exploration tests handle tile contact and invalid coordinates", () => {
    var mask = new ExplorationMask(100, 100, (x, y) => x == 30 && y == 30);
    Check(ExplorationMask.Covers(mask, new(30.5f, 30.5f), Vector2.One));
    Check(!mask.IsExplored(-1, 30) && !mask.IsExplored(100, 30));
    Check(!ExplorationMask.Covers(mask, new(float.NaN, 30), Vector2.One));
    Check(SpatialRules.ValidateReturn(new TestWorld(), new(10, 30), new(1, 2), exploration: mask) == ReturnFailure.OutOfWorld);
});
Run("Detaching the body does not suppress the world tick", () => {
    var body = new TestBody(); PlayerBodyState before = body.State;
    int worldTicks = 0;
    using (new DetachedBodyScope(body))
    {
        worldTicks++;
        Check(body.State.Ghost && body.State.Invisible && body.State.Immune);
        Check(body.State.Velocity == Vector2.Zero && body.State.ItemAnimation == 0 && body.State.ItemTime == 0);
        Check(body.State.Position == before.Position);
    }
    Check(worldTicks == 1 && body.State == before);
});
Run("Body flags and position are restored when a world tick throws", () => {
    var body = new TestBody(); PlayerBodyState before = body.State;
    try
    {
        using var detached = new DetachedBodyScope(body);
        body.State = body.State with { Position = new(600, 600), Velocity = new(10, 10) };
        throw new InvalidOperationException("Simulated world failure");
    }
    catch (InvalidOperationException) { }
    Check(body.State == before);
});
Run("Repeated body detachment does not accumulate spectator flags", () => {
    var body = new TestBody(); PlayerBodyState before = body.State;
    for (int tick = 0; tick < 120; tick++)
    {
        using (new DetachedBodyScope(body)) Check(body.State.Ghost);
        Check(body.State == before);
    }
});
Run("Disposing a body lease twice does not undo subsequent changes", () => {
    var body = new TestBody(); var detached = new DetachedBodyScope(body);
    detached.Dispose(); body.State = body.State with { Position = new(80, 40), Invisible = true };
    detached.Dispose(); Check(body.State.Position == new Vector2(80, 40) && body.State.Invisible && !body.State.Ghost);
});
Run("Departure recovery rechecks newly placed foreground blocks", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Solid);
    var mask = new ExplorationMask(100, 100, (_, _) => true);
    Check(ReturnSearch.TryNear(world, new(30.5f, 30.5f), new(1, 2), mask, out Vector2 safe));
    Check(safe != new Vector2(30.5f, 30.5f));
    Check(SpatialRules.ValidateReturn(world, safe, new(1, 2), exploration: mask) == ReturnFailure.None);
});
Run("Departure recovery rejects new liquid and stays in explored space", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Empty, HasLiquid: true);
    var mask = new ExplorationMask(100, 100, (x, y) => x >= 29 && x <= 31 && y >= 27 && y <= 29);
    Check(ReturnSearch.TryNear(world, new(30.5f, 30.5f), new(1, 2), mask, out Vector2 safe));
    Check(SpatialRules.ValidateReturn(world, safe, new(1, 2), exploration: mask) == ReturnFailure.None);
});
Run("An obstructed departure point cannot recover into unexplored neighbors", () => {
    var world = new TestWorld(); world[30, 30] = new(BlockShape.Solid);
    var mask = new ExplorationMask(100, 100, (_, _) => false);
    Check(!ReturnSearch.TryNear(world, new(30.5f, 30.5f), new(1, 2), mask, out _, radius: 5));
});
Run("Departure recovery reports failure when no safe space exists", () => {
    var world = new TestWorld();
    for (int x = 20; x <= 40; x++)
    for (int y = 20; y <= 40; y++) world[x, y] = new(BlockShape.Solid);
    Check(!ReturnSearch.TryNear(world, new(30.5f, 30.5f), new(1, 2), null, out _, radius: 5));
});
Run("An unchanged departure point remains an exact recovery exit", () => {
    var mask = new ExplorationMask(100, 100, (_, _) => false);
    Check(ReturnSearch.TryNear(new TestWorld(), new(30.5f, 30.5f), new(1, 2), mask, out Vector2 safe));
    Check(safe == new Vector2(30.5f, 30.5f));
});
Run("Entering AI exclusion removes an existing player lock and its aim area", () => {
    var player = new TestTargetPlayer();
    var memory = new TestAggro { Target = 0, PreviousTarget = 0, HasAimArea = true };
    using (new TargetExclusionScope(player, 0, memory))
    {
        Check(!player.Presence.Active && player.Presence.Dead && player.Presence.Ghost);
        Check(memory.Target == TargetExclusionScope.NoPlayer && memory.PreviousTarget == TargetExclusionScope.NoPlayer && !memory.HasAimArea);
    }
    Check(player.Presence == new TargetPresence(true, false, false));
    Check(memory.Target == TargetExclusionScope.NoPlayer);
});
Run("A legacy AI reacquiring player zero cannot retain that target", () => {
    var player = new TestTargetPlayer();
    var memory = new TestAggro { Target = TargetExclusionScope.NoPlayer, PreviousTarget = TargetExclusionScope.NoPlayer };
    using (new TargetExclusionScope(player, 0, memory))
    {
        // Simulate a legacy fallback or mod assigning a target directly during AI.
        memory.Target = memory.PreviousTarget = 0;
        memory.HasAimArea = true;
    }
    Check(memory.Target == TargetExclusionScope.NoPlayer && memory.PreviousTarget == TargetExclusionScope.NoPlayer && !memory.HasAimArea);
    Check(player.Presence.Active && !player.Presence.Dead);
});
Run("NPC-versus-NPC combat targets survive spectator exclusion", () => {
    var memory = new TestAggro { Target = 348, PreviousTarget = 0, HasAimArea = true };
    using (new TargetExclusionScope(new TestTargetPlayer(), 0, memory))
        Check(memory.Target == 348 && memory.HasAimArea);
    Check(memory.Target == 348 && memory.HasAimArea && memory.PreviousTarget == TargetExclusionScope.NoPlayer);
});
Run("Another player's valid target is not erased", () => {
    var memory = new TestAggro { Target = 7, PreviousTarget = 7, HasAimArea = true };
    using (new TargetExclusionScope(new TestTargetPlayer(), 0, memory)) { }
    Check(memory.Target == 7 && memory.PreviousTarget == 7 && memory.HasAimArea);
});
Run("A stale player aim area is cleared even after the target became empty", () => {
    var memory = new TestAggro { Target = TargetExclusionScope.NoPlayer, PreviousTarget = 0, HasAimArea = true };
    TargetExclusionScope.ForgetPlayer(memory, 0);
    Check(!memory.HasAimArea && memory.PreviousTarget == TargetExclusionScope.NoPlayer);
});
Run("An AI exception restores player presence while dropping its renewed lock", () => {
    var player = new TestTargetPlayer();
    var memory = new TestAggro { Target = 0, PreviousTarget = 0, HasAimArea = true };
    try
    {
        using var excluded = new TargetExclusionScope(player, 0, memory);
        memory.Target = 0;
        throw new InvalidOperationException("Simulated AI failure");
    }
    catch (InvalidOperationException) { }
    Check(player.Presence == new TargetPresence(true, false, false) && memory.Target == TargetExclusionScope.NoPlayer);
});
Run("Nested target searches do not make the player available inside AI", () => {
    var player = new TestTargetPlayer();
    using (new TargetExclusionScope(player, 0))
    {
        using (new TargetExclusionScope(player, 0)) Check(!player.Presence.Active);
        Check(!player.Presence.Active && player.Presence.Dead && player.Presence.Ghost);
    }
    Check(player.Presence == new TargetPresence(true, false, false));
});
Run("The no-player sentinel is unavailable during legacy AI only", () => {
    var none = new TestTargetPlayer { Presence = new TargetPresence(false, false, false) };
    using (new TargetExclusionScope(none, 0))
        Check(none.Presence == new TargetPresence(false, true, true));
    Check(none.Presence == new TargetPresence(false, false, false));
});
Run("Repeated AI exclusion preserves the outer world observation presence", () => {
    var player = new TestTargetPlayer { Presence = new TargetPresence(true, false, true) };
    for (int tick = 0; tick < 120; tick++)
    {
        using (new TargetExclusionScope(player, 0)) Check(!player.Presence.Active && player.Presence.Dead);
        Check(player.Presence == new TargetPresence(true, false, true));
    }
});
Run("A save during excluded AI serializes an alive player and restores AI state", () => {
    var player = new TestTargetPlayer();
    using (new TargetExclusionScope(player, 0))
    {
        TargetExclusionScope.SerializeAlive(player, () => Check(player.Presence == new TargetPresence(true, false, false)));
        Check(player.Presence == new TargetPresence(false, true, true));
    }
    Check(player.Presence == new TargetPresence(true, false, false));
});
Run("A failed save does not leak temporary player availability into AI", () => {
    var player = new TestTargetPlayer();
    using (new TargetExclusionScope(player, 0))
    {
        try
        {
            TargetExclusionScope.SerializeAlive(player, () => throw new InvalidOperationException("Simulated save failure"));
        }
        catch (InvalidOperationException) { }
        Check(player.Presence == new TargetPresence(false, true, true));
    }
    Check(player.Presence == new TargetPresence(true, false, false));
});
Run("Detached players cannot be discovered even at touching distance", () => {
    Check(!Perceives(1, detached: true));
    Check(!Perceives(1, detached: true, tracking: true));
});
Run("A fresh target requires discovery distance and line of sight", () => {
    Check(Perceives(40 * 16)); Check(!Perceives(40 * 16 + 1));
    Check(!Perceives(10, visible: false));
});
Run("Existing pursuit has a larger loss range and survives a temporary occlusion", () => {
    Check(Perceives(60 * 16, tracking: true, visible: false));
    Check(Perceives(64 * 16, tracking: true)); Check(!Perceives(64 * 16 + 1, tracking: true));
});
Run("A distant dimensional return cannot restore the old pursuit", () => {
    Check(!Perceives(60000, tracking: false));
    Check(!Perceives(60000, tracking: true));
    Check(!Perceives(50 * 16, tracking: false));
    Check(Perceives(30 * 16, tracking: false));
});
Run("Inactive dead and ghost players are never valid targets", () => {
    foreach (TargetPresence presence in new[] { new TargetPresence(false, false, false), new(true, true, false), new(true, false, true) })
        Check(!EnemyPerception.CanTarget(Vector2.Zero, Vector2.One, presence, false, true, true, 640, 1024));
});
Run("Enemy perception uses radial distance and rejects invalid coordinates", () => {
    Check(!EnemyPerception.CanTarget(Vector2.Zero, new(500, 500), new(true, false, false), false, false, true, 640, 1024));
    Check(!EnemyPerception.CanTarget(new(float.NaN, 0), Vector2.Zero, new(true, false, false), false, true, true, 640, 1024));
});
Run("A loss distance below discovery does not produce repeated aggro flicker", () => {
    Check(EnemyPerception.CanTarget(Vector2.Zero, new(500, 0), new(true, false, false), false, true, false, 640, 320));
});
Run("Ground wander alternates resting and movement without travelling forever", () => {
    var idle = new IdleWander(Vector2.Zero, 123);
    Vector2 position = Vector2.Zero, velocity = new(8, 0);
    bool left = false, right = false, stopped = false;
    for (int i = 0; i < 18000; i++)
    {
        IdleMotion next = idle.Step(IdleMovement.Walk, new(position, velocity, true));
        velocity = next.Velocity; position += velocity;
        left |= velocity.X < -0.5f; right |= velocity.X > 0.5f; stopped |= i > 100 && velocity.X == 0;
        Check(Math.Abs(position.X) < 115);
    }
    Check(left && right && stopped);
});
Run("Flying wander remains near its starting area for five simulated minutes", () => {
    var idle = new IdleWander(new(400, 300), 654);
    Vector2 position = idle.Home, velocity = new(14, -9);
    bool left = false, right = false;
    for (int i = 0; i < 18000; i++)
    {
        IdleMotion next = idle.Step(IdleMovement.Fly, new(position, velocity, false, UsesGravity: false));
        velocity = next.Velocity; position += velocity;
        left |= velocity.X < -0.2f; right |= velocity.X > 0.2f;
        Check(Math.Abs(position.X - 400) < 90 && Math.Abs(position.Y - 300) < 60);
    }
    Check(left && right);
});
Run("Slimes hop and settle without a player rather than jumping endlessly in one direction", () => {
    var idle = new IdleWander(Vector2.Zero, 321);
    Vector2 position = Vector2.Zero, velocity = Vector2.Zero;
    int jumps = 0; bool left = false, right = false;
    for (int i = 0; i < 18000; i++)
    {
        bool grounded = position.Y == 0 && velocity.Y >= 0;
        IdleMotion next = idle.Step(IdleMovement.Hop, new(position, velocity, grounded));
        if (grounded && next.Velocity.Y < 0) jumps++;
        velocity = next.Velocity; velocity.Y += 0.3f; position += velocity;
        if (position.Y >= 0) { position.Y = 0; velocity.Y = 0; }
        left |= velocity.X < -0.5f; right |= velocity.X > 0.5f;
        Check(Math.Abs(position.X) < 150);
    }
    Check(jumps > 30 && left && right);
});
Run("Ground enemies stop momentum toward an unsupported ledge", () => {
    var idle = new IdleWander(Vector2.Zero, 8);
    IdleMotion next = idle.Step(IdleMovement.Walk, new(Vector2.Zero, new(5, 0), true, CanRight: false));
    Check(next.Velocity.X <= 0);
});
Run("A trapped ground enemy stays still instead of pushing forever", () => {
    var idle = new IdleWander(Vector2.Zero, 8);
    Vector2 velocity = new(8, 0);
    for (int i = 0; i < 1000; i++)
    {
        velocity = idle.Step(IdleMovement.Walk, new(Vector2.Zero, velocity, true, BlockedX: true, CanLeft: false, CanRight: false)).Velocity;
        Check(velocity.X == 0);
    }
});
Run("Swimming wander cannot push out of the surrounding water", () => {
    var idle = new IdleWander(Vector2.Zero, 8);
    Vector2 velocity = new(4, -4);
    for (int i = 0; i < 1000; i++)
    {
        velocity = idle.Step(IdleMovement.Swim, new(Vector2.Zero, velocity, false,
            CanLeft: false, CanRight: false, CanUp: false, CanDown: false, UsesGravity: false)).Velocity;
        Check(velocity == Vector2.Zero);
    }
});
Run("Stationary enemies brake without cancelling native falling", () => {
    var idle = new IdleWander(Vector2.Zero, 8);
    Vector2 velocity = new(10, 6);
    for (int i = 0; i < 120; i++) velocity = idle.Step(IdleMovement.Stationary, new(Vector2.Zero, velocity, false)).Velocity;
    Check(velocity.X == 0 && velocity.Y == 6);
});
Run("A new no-target episode anchors at the enemy's new position", () => {
    var first = new IdleWander(new(100, 100), 8);
    var next = new IdleWander(new(800, 300), 9);
    Check(next.Home == new Vector2(800, 300) && first.Home != next.Home);
});
Run("Held sprites preserve both wide and tall animation-frame proportions", () => {
    HeldItemLayout wide = HeldItemLayout.Create(80, 20, false, true);
    HeldItemLayout tall = HeldItemLayout.Create(12, 48, true, false);
    Check(Math.Abs(wide.Size.X / wide.Size.Y - 4) < 0.0001f);
    Check(Math.Abs(tall.Size.X / tall.Size.Y - 0.25f) < 0.0001f);
});
Run("Tiny and huge held sprites have a bounded camera-space size", () => {
    Check(Math.Abs(HeldItemLayout.Create(8, 8, false, false).Size.X - 0.322f) < 0.0001f);
    Check(Math.Abs(HeldItemLayout.Create(2048, 2048, false, false).Size.X - 0.805f) < 0.0001f);
    Check(HeldItemLayout.Create(0, 16, false, false).Size == Vector2.Zero);
});
Run("Each held item grip stays exactly at the shared hand attachment", () => {
    foreach (HeldItemLayout layout in new[] { HeldItemLayout.Create(32, 32, false, false),
        HeldItemLayout.Create(48, 48, true, false), HeldItemLayout.Create(80, 20, false, true) })
        Check(layout.At(layout.Grip.X, layout.Grip.Y) == Vector2.Zero);
});
Run("Narrow projections pull the shared hand and item inward", () => {
    float normal = HeldItemLayout.HandOffsetX(0.75f), narrow = HeldItemLayout.HandOffsetX(1.8f);
    Check(normal == 0.70f && narrow < normal && narrow > 0);
    Check(narrow * 1.8f <= 0.7201f);
    Check(float.IsFinite(HeldItemLayout.HandOffsetX(float.NaN)));
});
Run("Saving a legitimate ghost or dead character during distant-enemy AI preserves that state", () => {
    foreach (TargetPresence real in new[] { new TargetPresence(true, false, true), new(true, true, false) })
    {
        var player = new TestTargetPlayer { Presence = real };
        using (new TargetExclusionScope(player, 0))
        {
            TargetExclusionScope.SerializePresence(player, real, () => Check(player.Presence == real));
            Check(player.Presence == new TargetPresence(false, true, true));
        }
        Check(player.Presence == real);
    }
});
Run("Resting tools stay upright independently of the arm, viewport and sprite aspect", () => {
    foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
    foreach (float fov in new[] { 60f, 75f, 100f })
    foreach (float elapsed in new[] { 0f, 0.13f, 0.4f })
    foreach (Vector2 size in new[] { new Vector2(32, 32), new(16, 48), new(48, 24), new(64, 8) })
    {
        HeldItemLayout sword = HeldItemLayout.Create((int)size.X, (int)size.Y, true, false);
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fov * MathF.PI / 180, aspect, 0.05f, 420);
        Matrix4x4 hand = FirstPersonPose.Hand(projection, elapsed, true);
        Matrix4x4 item = FirstPersonPose.Item(sword, hand, projection);
        Vector2 tip = sword.At(sword.FlipHorizontally ? 0 : 1, 0);
        Vector2 blade = FirstPersonPose.Project(new(tip, 0), item, projection) - FirstPersonPose.Project(Vector3.Zero, item, projection);
        float angle = MathF.Atan2(blade.Y, blade.X) * 180 / MathF.PI;
        Check(Math.Abs(angle - 95) < 0.05f);
        Check(blade.Y > 0 && blade.X < 0);
    }
});
Run("Turning through all horizontal headings keeps W aligned with the rendered view", () => {
    foreach (float yaw in new[] { 0f, MathF.PI / 2, MathF.PI, -MathF.PI / 2, 0.83f })
    foreach (float pitch in new[] { -1.4f, 0f, 1.4f })
    {
        var flight = new FlightState(new(50, 50), new(1, 2));
        for (int i = 0; i < 30; i++) flight.Update(new TestWorld(), 1f / 60, Vector3.Zero, false);
        flight.Look(yaw, pitch); Vector3 start = flight.Position;
        flight.Update(new TestWorld(), 0.05f, Vector3.UnitZ, false);
        Check(Vector3.Dot(Vector3.Normalize(flight.Position - start), flight.Forward) > 0.9999f);
    }
});
Run("S flies opposite the complete facing direction", () => {
    var flight = new FlightState(new(50, 50), new(1, 2));
    for (int i = 0; i < 30; i++) flight.Update(new TestWorld(), 1f / 60, Vector3.Zero, false);
    flight.Look(0.6f, -1); Vector3 start = flight.Position;
    flight.Update(new TestWorld(), 0.05f, -Vector3.UnitZ, false);
    Check(Vector3.Dot(Vector3.Normalize(flight.Position - start), -flight.Forward) > 0.9999f);
});
Run("Following the view into the world still stops at the plane air wall", () => {
    var flight = new FlightState(new(50, 50), new(1, 2));
    for (int i = 0; i < 30; i++) flight.Update(new TestWorld(), 1f / 60, Vector3.Zero, false);
    flight.Look(0, 0.1f);
    for (int i = 0; i < 300; i++) flight.Update(new TestWorld(), 0.05f, Vector3.UnitZ, true);
    Check(flight.Position.Z >= SpatialRules.MinFlightDepth - 0.001f && flight.Position.Z < 1);
});
Run("Tool orientation changes do not move the grip away from the fingers", () => {
    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(75 * MathF.PI / 180, 16f / 9, 0.05f, 420);
    Matrix4x4 hand = FirstPersonPose.Hand(projection, 0.3f, true);
    Vector3 attachment = Vector3.Transform(FirstPersonPose.GripOffset, hand);
    foreach (Vector2 size in new[] { new Vector2(32, 32), new(16, 48), new(48, 24) })
    {
        HeldItemLayout layout = HeldItemLayout.Create((int)size.X, (int)size.Y, true, false);
        Matrix4x4 item = FirstPersonPose.Item(layout, hand, projection);
        Vector2 grip = layout.At(layout.Grip.X, layout.Grip.Y);
        Check(Vector3.Distance(Vector3.Transform(new(grip, 0), item), attachment) < 0.0001f);
    }
});
Run("The arm reaches inward from the bottom-right toward the center", () => {
    foreach (float aspect in new[] { 4f / 3, 16f / 9, 21f / 9 })
    foreach (float fov in new[] { 60f, 75f, 100f })
    {
        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(fov * MathF.PI / 180, aspect, 0.05f, 420);
        Matrix4x4 hand = FirstPersonPose.Hand(projection, 0, false);
        Vector2 shoulder = FirstPersonPose.Project(new(0, -0.65f, 0), hand, projection);
        Vector2 fingers = FirstPersonPose.Project(new(0, 0.24f, 0), hand, projection);
        Vector2 inward = Vector2.Normalize(fingers - shoulder);
        Check(shoulder.X > fingers.X && shoulder.Y < fingers.Y);
        Check(fingers.Length() < shoulder.Length());
        Check(Vector2.Dot(inward, Vector2.Normalize(-fingers)) > 0.9f);
    }
});
Run("The mining ray uses the same eye height as the rendered camera", () => {
    var flight = new FlightState(new(30, 30), new(1, 2));
    Check(flight.Eye.X == flight.Position.X && flight.Eye.Z == flight.Position.Z);
    Check(flight.Eye.Y == flight.Position.Y - 0.56f);
});
Run("A ray selects the first foreground block instead of its background wall", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, true, BlockShape.Solid);
    TileRayHit? hit = TileRaycast.Cast(world, new(30.5f, 30.5f, 4), -Vector3.UnitZ, 6);
    Check(hit is { X: 30, Y: 30, Layer: RayLayer.Tile });
    Check(Math.Abs(hit.GetValueOrDefault().Distance - 3.5f) < 0.0001f);
});
Run("Removing the foreground exposes the background wall to a hammer ray", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(false, true, BlockShape.Empty);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 4), -Vector3.UnitZ, 6) is { Layer: RayLayer.Wall });
});
Run("Rays cannot reach beyond their 3D tool distance", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Solid);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 7), -Vector3.UnitZ, 6) is null);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 6.5f), -Vector3.UnitZ, 6) is not null);
});
Run("Aiming away from the map does not select blocks behind the camera", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Solid);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 4), Vector3.UnitZ, 6) is null);
});
Run("Oblique rays pick the closest occupied voxel rather than the flight projection", () => {
    var world = new TestInteractionWorld(); world[32, 30] = new(true, false, BlockShape.Solid);
    TileRayHit? hit = TileRaycast.Cast(world, new(30.5f, 30.5f, 3), new(2, 0, -2.5f), 6);
    Check(hit is { X: 32, Y: 30 });
});
Run("Half-block and platform targeting follows their rendered empty space", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.HalfBlock);
    Check(TileRaycast.Cast(world, new(30.5f, 30.2f, 3), -Vector3.UnitZ, 6) is null);
    Check(TileRaycast.Cast(world, new(30.5f, 30.8f, 3), -Vector3.UnitZ, 6) is not null);
    world[30, 30] = new(true, false, BlockShape.Platform);
    Check(TileRaycast.Cast(world, new(30.5f, 30.08f, 3), -Vector3.UnitZ, 6) is not null);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 3), -Vector3.UnitZ, 6) is null);
});
Run("Non-solid trees and furniture remain targetable", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Empty);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 3), -Vector3.UnitZ, 6) is { Layer: RayLayer.Tile });
});
Run("Exploration fog blocks targeting without inspecting hidden tiles", () => {
    var world = new TestInteractionWorld { RejectHiddenReads = true };
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    Check(TileRaycast.Cast(world, new(30.5f, 30.5f, 3), -Vector3.UnitZ, 6, fog) is { Layer: RayLayer.Unexplored });
});
Run("Invalid rays and world edges do not index out of bounds", () => {
    var world = new TestInteractionWorld();
    Check(TileRaycast.Cast(world, new(float.NaN, 30, 3), -Vector3.UnitZ, 6) is null);
    Check(TileRaycast.Cast(world, new(30, 30, 3), Vector3.Zero, 6) is null);
    Check(TileRaycast.Cast(world, new(0, 0, 3), -Vector3.UnitZ, 6) is null);
});
Run("Interaction permission requires active single-player flight with no menu", () => {
    var ready = new InteractionContext(true, true, true, true, false, false);
    Check(ready.Allows(true) && !ready.Allows(false));
    Check(!(ready with { InFlight = false }).Allows(true));
    Check(!(ready with { SinglePlayer = false }).Allows(true));
    Check(!(ready with { Alive = false }).Allows(true));
    Check(!(ready with { Focused = false }).Allows(true));
    Check(!(ready with { Paused = true }).Allows(true));
    Check(!(ready with { InventoryOpen = true }).Allows(true));
});
Run("Mining requires releasing a held button after entering or closing a menu", () => {
    var input = new MiningInput();
    Check(!input.Tick(true, true, true));
    Check(!input.Tick(true, false, true)); Check(input.Tick(true, true, true));
    input.Suspend(); Check(!input.Tick(true, true, true));
    Check(!input.Tick(true, false, true)); Check(input.Tick(true, true, true));
});
Run("Holding mine repeats at tool cadence and does not run while disabled", () => {
    var input = new MiningInput(); input.Tick(true, false, true);
    Check(input.Tick(true, true, true)); input.Hit(3);
    Check(!input.Tick(true, true, true)); Check(!input.Tick(true, true, true)); Check(input.Tick(true, true, true));
    Check(!input.Tick(false, true, true)); Check(!input.Tick(true, true, true));
    input.Tick(true, false, false); Check(!input.Tick(true, true, false));
});
Run("Drop pickup uses actual flight depth instead of only its 2D projection", () => {
    Check(InteractionRules.CanReachDrop(new(30, 30, 1), new(30, 30), Vector2.One, 2, null));
    Check(!InteractionRules.CanReachDrop(new(30, 30, 32), new(30, 30), Vector2.One, 2, null));
    Check(!InteractionRules.CanReachDrop(new(33, 30, 1), new(30, 30), Vector2.One, 2, null));
});
Run("Pickup rejects unexplored footprints and invalid coordinates", () => {
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    Check(!InteractionRules.CanReachDrop(new(30, 30, 1), new(30, 30), Vector2.One, 2, fog));
    Check(!InteractionRules.CanReachDrop(new(float.NaN, 30, 1), new(30, 30), Vector2.One, 2, null));
});
Run("Mining swing rises, strikes and returns to its resting pose", () => {
    var swing = new MiningSwing(); swing.Start(12);
    Check(swing.Active && swing.Amount == 0);
    swing.Update(0.1f); Check(swing.Amount > 0.99f);
    swing.Update(0.1f); Check(!swing.Active && swing.Amount == 0);
});
Run("Fast repeated hits cannot pin the swing at its first animation frame", () => {
    var swing = new MiningSwing(); float maximum = 0;
    for (int i = 0; i < 30; i++) { swing.Start(1); swing.Update(1f / 60); maximum = Math.Max(maximum, swing.Amount); }
    Check(maximum > 0.9f);
    swing.Reset(); Check(!swing.Active && swing.Amount == 0);
});
Run("Swinging moves both the hand and the tool while preserving their grip", () => {
    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(75 * MathF.PI / 180, 16f / 9, 0.05f, 420);
    Matrix4x4 rest = FirstPersonPose.Hand(projection, 0, false);
    HeldItemLayout tool = HeldItemLayout.Create(32, 32, true, false);
    Matrix4x4 before = FirstPersonPose.Item(tool, rest, projection);
    Matrix4x4 struck = FirstPersonPose.Item(tool, rest, projection, 1);
    Vector3 expectedGrip = Vector3.Transform(FirstPersonPose.GripOffset, FirstPersonPose.Swing(1) * rest);
    Check(Vector3.Distance(Vector3.Transform(Vector3.Zero, struck), expectedGrip) < 0.0001f);
    Vector2 tip = tool.At(0, 0);
    Check(Vector3.Distance(Vector3.Transform(new(tip, 0), before), Vector3.Transform(new(tip, 0), struck)) > 0.08f);
    Check(FirstPersonPose.Item(tool, rest, projection, 0) == before);
});
Run("Mining fragments move outward, fall, fade and expire", () => {
    MiningDebris debris = new(new(30, 30, 0.6f), new(1, -2, 1), 0.5f);
    for (int i = 0; i < 5; i++) debris.Update(0.05f);
    Check(debris.Position.Z > 0.8f && debris.Position.X > 30 && debris.Position.Y < 30);
    Check(debris.Fade > 0 && debris.Fade < 1);
    for (int i = 0; i < 10; i++) debris.Update(0.05f);
    Check(!debris.Active && debris.Fade == 0);
});
Run("The enlarged default pickup radius reaches drops that the old radius missed", () => {
    Check(InteractionRules.PickupRadius == 6);
    Check(InteractionRules.CanReachDrop(new(34, 30, 2), new(30, 30), Vector2.One, InteractionRules.PickupRadius, null));
    Check(!InteractionRules.CanReachDrop(new(37, 30, 2), new(30, 30), Vector2.One, InteractionRules.PickupRadius, null));
    Check(InteractionRules.CanReachDrop(new(37, 30, 2), new(30, 30), Vector2.One, 10, null));
});
Run("A small torch extends above the top of the palm", () => {
    HeldItemLayout torch = HeldItemLayout.Create(10, 20, false, false);
    float top = FirstPersonPose.GripOffset.Y + torch.At(0.5f, 0).Y;
    float bottom = FirstPersonPose.GripOffset.Y + torch.At(0.5f, 1).Y;
    Check(top > 0.40f && bottom > 0.16f);
    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(75 * MathF.PI / 180, 16f / 9, 0.05f, 420);
    Matrix4x4 hand = FirstPersonPose.Hand(projection, 0, false);
    Matrix4x4 oldHand = hand; oldHand.M41 = 0.50f; oldHand.M42 = -0.44f; oldHand.M43 = -1.05f;
    Vector2 fingers = FirstPersonPose.Project(new(0, 0.24f, 0), hand, projection);
    Vector2 before = FirstPersonPose.Project(new(0, 0.24f, 0), oldHand, projection);
    Check(fingers.X > before.X && fingers.Y < before.Y);
});
Run("Aimed attacks pick the closest eligible creature rather than slot order", () => {
    CombatBody[] bodies = { new(1, new(30, 30), Vector2.One, true, true), new(2, new(30, 30), Vector2.One, true) };
    Check(CombatRules.Target(bodies, new(30, 30, 4), -Vector3.UnitZ, 6, 6, null) is { Id: 2 });
    Check(CombatRules.Target(bodies.Reverse(), new(30, 30, 4), -Vector3.UnitZ, 6, 6, null) is { Id: 2 });
});
Run("Melee reach includes flight depth and cannot hit distant sheet enemies", () => {
    CombatBody[] bodies = { new(1, new(30, 30), Vector2.One, true) };
    Check(CombatRules.Target(bodies, new(30, 30, 4), -Vector3.UnitZ, CombatRules.MeleeReach, 20, null) is not null);
    Check(CombatRules.Target(bodies, new(30, 30, 8), -Vector3.UnitZ, CombatRules.MeleeReach, 20, null) is null);
    Check(CombatRules.Target(bodies, new(30, 30, 32), -Vector3.UnitZ, CombatRules.ShotReach, 48, null) is not null);
    Check(CombatRules.Target(bodies, new(30, 30, 50), -Vector3.UnitZ, CombatRules.ShotReach, 48, null) is null);
});
Run("Compressed melee depth doubles straight-on reach while preserving planar reach", () => {
    CombatBody[] bodies={new(1,new(30,30),Vector2.One,true)};
    Check(CombatRules.Target(bodies,new(30,30,8),-Vector3.UnitZ,CombatRules.MeleeReach,20,null,CombatRules.MeleeDepthWeight) is not null);
    Check(CombatRules.Target(bodies,new(30,30,10),-Vector3.UnitZ,CombatRules.MeleeReach,20,null,CombatRules.MeleeDepthWeight) is null);
    Check(CombatRules.Target(bodies,new(36,30,.57f),-Vector3.UnitX,CombatRules.MeleeReach,20,null,CombatRules.MeleeDepthWeight) is null);
    Check(Math.Abs(CombatRules.RayReach(-Vector3.UnitZ,4.5f,.5f)-9)<0.001f);
    Check(Math.Abs(CombatRules.RayReach(Vector3.UnitX,4.5f,.5f)-4.5f)<0.001f);
    Vector3 diagonal=Vector3.Normalize(new Vector3(1,0,-1));
    Check(Math.Abs(CombatRules.RayReach(diagonal,4.5f,.5f)-4.5f/MathF.Sqrt(.625f))<0.001f);
});
Run("Weighted melee keeps real hit distance, terrain ordering and exploration restrictions", () => {
    CombatBody[] bodies={new(1,new(30.5f,30.5f),Vector2.One,true)};
    Vector3 eye=new(30.5f,30.5f,8);
    var hit=CombatRules.Target(bodies,eye,-Vector3.UnitZ,4.5f,20,null,.5f)!.Value;
    Check(hit.Distance>7 && hit.Point.Z>.5f && hit.Point.Z<.7f);
    Check(CombatRules.Target(bodies,eye,-Vector3.UnitZ,4.5f,7,null,.5f) is null);
    var hidden=new ExplorationMask(100,100,(_,_)=>false);
    Check(CombatRules.Target(bodies,eye,-Vector3.UnitZ,4.5f,20,hidden,.5f) is null);
    foreach(float invalid in new[]{0,-1,float.NaN,float.PositiveInfinity,2})
        Check(CombatRules.Target(bodies,eye,-Vector3.UnitZ,4.5f,20,null,invalid) is null);
});
Run("A nearer block or fog volume prevents hitting a creature behind it", () => {
    CombatBody[] bodies = { new(1, new(30.5f, 30.5f), Vector2.One, true, true) };
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Solid);
    Vector3 eye = new(30.5f, 30.5f, 4);
    float obstruction = TileRaycast.Cast(world, eye, -Vector3.UnitZ, 6)!.Value.Distance;
    Check(CombatRules.Target(bodies, eye, -Vector3.UnitZ, 6, obstruction, null) is null);
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    obstruction = TileRaycast.Cast(world, eye, -Vector3.UnitZ, 6, fog)!.Value.Distance;
    Check(CombatRules.Target(bodies, eye, -Vector3.UnitZ, 6, obstruction, fog) is null);
});
Run("Visible creatures in front of the sheet remain hittable", () => {
    CombatBody[] bodies = { new(1, new(30.5f, 30.5f), Vector2.One, true) };
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Solid);
    Vector3 eye = new(30.5f, 30.5f, 4);
    float obstruction = TileRaycast.Cast(world, eye, -Vector3.UnitZ, 6)!.Value.Distance;
    Check(CombatRules.Target(bodies, eye, -Vector3.UnitZ, 6, obstruction, null) is not null);
});
Run("Combat rejects friendlies, off-crosshair targets and rays behind the camera", () => {
    CombatBody[] bodies = { new(1, new(30, 30), Vector2.One, false), new(2, new(33, 30), Vector2.One, true) };
    Check(CombatRules.Target(bodies, new(30, 30, 4), -Vector3.UnitZ, 6, 6, null) is null);
    Check(CombatRules.Target(new[] { bodies[1] }, new(33, 30, 4), Vector3.UnitZ, 6, 6, null) is null);
});
Run("Combat requires the entire creature footprint to have been explored", () => {
    CombatBody[] bodies = { new(1, new(30.5f, 30.5f), new(2, 2), true) };
    var partial = new ExplorationMask(100, 100, (x, y) => x == 30 && y == 30);
    var explored = new ExplorationMask(100, 100, (_, _) => true);
    Check(CombatRules.Target(bodies, new(30.5f, 30.5f, 4), -Vector3.UnitZ, 6, 6, partial) is null);
    Check(CombatRules.Target(bodies, new(30.5f, 30.5f, 4), -Vector3.UnitZ, 6, 6, explored) is not null);
});
Run("Invalid combat coordinates or distances cannot produce hits", () => {
    CombatBody[] bodies = { new(1, new(30, 30), Vector2.One, true) };
    Check(CombatRules.Target(bodies, new(float.NaN, 30, 4), -Vector3.UnitZ, 6, 6, null) is null);
    Check(CombatRules.Target(bodies, new(30, 30, 4), Vector3.Zero, 6, 6, null) is null);
    Check(CombatRules.Target(bodies, new(30, 30, 4), -Vector3.UnitZ, 6, float.NaN, null) is null);
});
Run("Left mouse chooses exactly one nearest mining or combat action", () => {
    Check(CombatRules.Choose(true, true, 3, true, true, 2) == PrimaryAction.Attack);
    Check(CombatRules.Choose(true, true, 2, true, true, 3) == PrimaryAction.Mine);
    Check(CombatRules.Choose(true, true, 2, true, true, null) == PrimaryAction.Mine);
    Check(CombatRules.Choose(true, false, 2, true, true, null) == PrimaryAction.Attack);
});
Run("Mining and combat switches remain independent", () => {
    Check(CombatRules.Choose(true, true, 3, false, true, 2) == PrimaryAction.Mine);
    Check(CombatRules.Choose(false, false, null, true, true, 2) == PrimaryAction.Attack);
    Check(CombatRules.Choose(false, false, null, false, true, 2) == PrimaryAction.None);
    Check(CombatRules.Choose(false, false, null, true, false, null) == PrimaryAction.None);
});
Run("Attack cooldown also prevents an immediate mining action", () => {
    var input = new MiningInput(); input.Tick(true, false, true);
    Check(input.Tick(true, true, true)); input.Hit(3);
    Check(!input.Tick(true, true, true)); Check(!input.Tick(true, true, true)); Check(input.Tick(true, true, true));
    input.Suspend(); Check(!input.Tick(true, true, true));
});
Run("Creature health bars clamp empty, full, overhealed and invalid maximum health", () => {
    Check(CreatureOverlayRules.HealthFraction(9999, 9999) == 1);
    Check(Math.Abs(CreatureOverlayRules.HealthFraction(9000, 9999) - 0.90009f) < 0.00001f);
    Check(CreatureOverlayRules.HealthFraction(-1, 9999) == 0);
    Check(CreatureOverlayRules.HealthFraction(12000, 9999) == 1);
    Check(CreatureOverlayRules.HealthFraction(9999, 0) == 0);
});
Run("World creature labels project to the UI center at different UI scales", () => {
    Matrix4x4 view = Matrix4x4.CreateLookAt(new(0, 0, 10), Vector3.Zero, Vector3.UnitY);
    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(75 * MathF.PI / 180, 16f / 9, 0.05f, 420);
    Check(CreatureOverlayRules.Project(Vector3.Zero, view * projection, 1920, 1080, out Vector2 normal));
    Check(CreatureOverlayRules.Project(Vector3.Zero, view * projection, 1280, 720, out Vector2 scaled));
    Check(Vector2.Distance(normal, new(960, 540)) < 0.001f);
    Check(Vector2.Distance(scaled * 1.5f, normal) < 0.001f);
});
Run("Creature label projection rejects behind-camera, far and offscreen points", () => {
    Matrix4x4 view = Matrix4x4.CreateLookAt(new(0, 0, 10), Vector3.Zero, Vector3.UnitY);
    Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(75 * MathF.PI / 180, 16f / 9, 0.05f, 420);
    foreach (Vector3 point in new[] { new Vector3(0, 0, 20), new(0, 0, -500), new(100, 0, 0), new(float.NaN, 0, 0) })
        Check(!CreatureOverlayRules.Project(point, view * projection, 1920, 1080, out _));
    Check(!CreatureOverlayRules.Project(Vector3.Zero, view * projection, float.NaN, 1080, out _));
});
Run("Damage numbers rise in world space, fade and expire without an NPC reference", () => {
    var feedback = new CombatFeedback();
    feedback.Add(new(30, 30, 0.63f), 999, false);
    DamagePopup popup = feedback.Popups[0];
    Check(popup.Damage == 999 && popup.Opacity == 1 && popup.Position == popup.Origin);
    feedback.Update(0.7f);
    Check(popup.Position.Y < 30 && popup.Position.X == 30 && popup.Position.Z == 0.63f);
    Check(popup.Opacity > 0 && popup.Opacity < 1);
    feedback.Update(0.2f);
    Check(feedback.Popups.Count == 0 && !popup.Active);
});
Run("Critical damage feedback lasts longer and can retain its original visibility anchor", () => {
    var feedback = new CombatFeedback();
    feedback.Add(new(30, 29, 0.63f), 1998, true, new(30, 30, 0.57f));
    DamagePopup popup = feedback.Popups[0];
    feedback.Update(0.9f);
    Check(popup.Active && popup.Critical && popup.VisibilityOrigin == new Vector3(30, 30, 0.57f));
    feedback.Update(0.3f); Check(feedback.Popups.Count == 0);
});
Run("Many hits have bounded popup memory and clearing flight removes old feedback", () => {
    var feedback = new CombatFeedback();
    for (int i = 1; i <= 1000; i++) feedback.Add(new(i, 30, 0.63f), i, false);
    Check(feedback.Popups.Count == 64 && feedback.Popups[0].Damage == 937 && feedback.Popups[^1].Damage == 1000);
    feedback.Clear(); Check(feedback.Popups.Count == 0);
});
Run("Invalid damage feedback cannot create misleading numbers or break lifetime updates", () => {
    var feedback = new CombatFeedback();
    feedback.Add(Vector3.Zero, 0, false);
    feedback.Add(new(float.NaN, 30, 1), 10, false);
    feedback.Add(Vector3.Zero, 10, true, new(float.PositiveInfinity, 0, 0));
    Check(feedback.Popups.Count == 0);
    feedback.Add(new(30, 30, 0.63f), 999, false);
    feedback.Update(float.NaN); feedback.Update(-1);
    Check(feedback.Popups[0].Age == 0);
});
Run("Sky colors remain continuous across dawn and dusk clock changes", () => {
    Check(Vector3.Distance(SkyPalette.Sample(true, 54000).Top, SkyPalette.Sample(false, 0).Top) < 0.00001f);
    Check(Vector3.Distance(SkyPalette.Sample(false, 32400).Horizon, SkyPalette.Sample(true, 0).Horizon) < 0.00001f);
    Check(SkyPalette.Sample(true, 27000).Top.Y > SkyPalette.Sample(false, 16000).Top.Y);
    Check(SkyPalette.Sample(true, double.NaN) == SkyPalette.Sample(true, 0));
});
Run("Sky transitions stay bounded and smooth throughout the complete clock", () => {
    foreach (bool day in new[] { true, false })
    {
        SkyPalette previous = SkyPalette.Sample(day, 0);
        for (int t = 60; t <= (day ? 54000 : 32400); t += 60)
        {
            SkyPalette next = SkyPalette.Sample(day, t);
            Check(next.Top.X >= 0 && next.Horizon.Y <= 1 && next.Top.Z <= 1);
            Check(Vector3.Distance(previous.Top, next.Top) < 0.05f && Vector3.Distance(previous.Horizon, next.Horizon) < 0.05f);
            previous = next;
        }
    }
});
Run("Projected backgrounds follow XY biome material and depth rather than the departure player", () => {
    var world = new EnvironmentWorld();
    Check(ProjectedEnvironment.Sample(world, new(1000, 300), 400, 800).Biome == ProjectedBiome.Forest);
    Check(ProjectedEnvironment.Sample(world, new(2000, 300), 400, 800).Biome == ProjectedBiome.Snow);
    Check(ProjectedEnvironment.Sample(world, new(2000, 600), 400, 800).Depth == ProjectedDepth.Underground);
    Check(ProjectedEnvironment.Sample(world, new(2000, 1000), 400, 800).Depth == ProjectedDepth.Cavern);
    Check(ProjectedEnvironment.Sample(world, new(2000, 1850), 400, 800).Depth == ProjectedDepth.Underworld);
    Check(ProjectedEnvironment.Sample(world, new(1000, 100), 400, 800).Depth == ProjectedDepth.Sky);
    Check(ProjectedEnvironment.Sample(world, new(100, 300), 400, 800).Biome == ProjectedBiome.Ocean);
});
Run("Invalid background projections do not read world tiles", () => {
    var world = new EnvironmentWorld { RejectReads = true };
    Check(ProjectedEnvironment.Sample(world, new(float.NaN, 300), 400, 800).Biome == ProjectedBiome.Unknown);
    Check(ProjectedEnvironment.Sample(world, new(-1, 300), 400, 800).Biome == ProjectedBiome.Unknown);
});
Run("Backgrounds recognize snow at unexplored projections while fog still rejects returns", () => {
    var world = new EnvironmentWorld();
    var fog = new ExplorationMask(world.Width, world.Height, (_, _) => false);
    Check(!fog.IsExplored(2000, 300));
    Check(ProjectedEnvironment.Sample(world, new(2000, 300), 400, 800).Biome == ProjectedBiome.Snow);
    Check(!ExplorationMask.Covers(fog, new(2000, 300), new(1.25f, 2.625f)));
});
Run("Sprite sheets isolate eight poses even when dimensions do not divide evenly", () => {
    SpriteCrop[] crops = SpriteSheetLayout.Slice(1254, 1254, 4, 2, (x, y) => (byte)(x % 313 > 20 && y % 627 > 30 ? 255 : 0));
    Check(crops.Length == 8);
    for (int i = 0; i < crops.Length; i++)
    {
        SpriteCrop crop = crops[i];
        Check(!crop.Empty && crop.X >= i % 4 * 1254 / 4 && crop.X + crop.Width <= (i % 4 + 1) * 1254 / 4);
        Check(crop.Y >= i / 4 * 1254 / 2 && crop.Y + crop.Height <= (i / 4 + 1) * 1254 / 2);
        Check(crop.FootX >= 0 && crop.FootX < crop.Width);
    }
});
Run("Transparent sprite cells stay empty and feet anchor asymmetric poses", () => {
    SpriteCrop[] crops = SpriteSheetLayout.Slice(20, 20, 2, 1, (x, y) => (byte)(x >= 1 && x <= 5 && y >= 2 && y <= 18 ? 255 : 0));
    Check(crops[1].Empty && crops[0].Width == 5 && crops[0].Height == 17);
    Check(Math.Abs(crops[0].FootX - 2) < 0.001f);
    Check(SpriteSheetLayout.Slice(1, 1, 4, 2, (_, _) => 255).Length == 0);
});
Run("High-Dimensional Projection returns to normal damage after leaving 3D", () => {
    Check(ProjectionWeaponStats.Damage(false) == 30);
    Check(ProjectionWeaponStats.Damage(true) == 999);
    Check(ProjectionWeaponStats.Damage(false) == 30);
});
Run("Ranged block power has a floor for weak guns and a cap for debug damage", () => {
    Check(ProjectionWeaponStats.ShotPickPower(7) == 35);
    Check(ProjectionWeaponStats.ShotPickPower(100) == 100);
    Check(ProjectionWeaponStats.ShotPickPower(999) == 200);
});
Run("Hit feedback events survive popup expiry so fallback cannot duplicate native hits", () => {
    var feedback = new CombatFeedback(); feedback.Add(Vector3.Zero, 30, false);
    long count = feedback.Events; feedback.Update(2);
    Check(count == 1 && feedback.Events == count && feedback.Popups.Count == 0);
    feedback.Add(Vector3.Zero, 0, false); Check(feedback.Events == count);
});
Run("A 1x1 async texture dummy cannot be selected as a Steve or sheep pose", () => {
    SpriteCrop[] pending = SpriteSheetLayout.Slice(1, 1, 4, 2, (_, _) => throw new InvalidOperationException("A dummy should not be read as an atlas"));
    for (int pose = 0; pose < 8; pose++) Check(!SpriteSheetLayout.TryFrame(pending, pose, out _));
});
Run("Frame selection rejects negative indices, missing poses and empty cells", () => {
    SpriteCrop[] poses = { new(1, 2, 10, 20, 5), default };
    Check(SpriteSheetLayout.TryFrame(poses, 0, out var frame) && frame.Height == 20);
    foreach (int index in new[] { -1, int.MinValue, 1, 2, 6, 7, int.MaxValue })
        Check(!SpriteSheetLayout.TryFrame(poses, index, out _));
});
Run("Real sheet poses remain available after the first pending texture is replaced", () => {
    SpriteCrop[] pending = SpriteSheetLayout.Slice(1, 1, 4, 2, (_, _) => 0);
    Check(!SpriteSheetLayout.TryFrame(pending, 0, out _));
    SpriteCrop[] ready = SpriteSheetLayout.Slice(40, 20, 4, 2, (_, _) => 255);
    for (int pose = 0; pose < 8; pose++) Check(SpriteSheetLayout.TryFrame(ready, pose, out var frame) && frame.Width == 10 && frame.Height == 10);
});
Run("Sheep eggs require ground and place the full animal immediately above it", () => {
    var world = new TestWorld();
    Check(!GroundSpawnRules.SheepPosition(world, 30, 50, out _));
    world[30, 50] = new(BlockShape.Solid, false, false);
    Check(GroundSpawnRules.SheepPosition(world, 30, 50, out var center));
    Check(Math.Abs(center.Y + GroundSpawnRules.SheepSize.Y / 2 - 50) < 0.001f);
});
Run("Sheep eggs reject a narrow space, low ceiling or liquid without clipping the body", () => {
    var world = new TestWorld(); world[30, 50] = new(BlockShape.Solid, false, false);
    world[29, 49] = new(BlockShape.Solid, false, false);
    Check(!GroundSpawnRules.SheepPosition(world, 30, 50, out _));
    world[29, 49] = default;
    world[30, 48] = new(BlockShape.Solid, false, false);
    Check(!GroundSpawnRules.SheepPosition(world, 30, 50, out _));
    world[30, 48] = default;
    world[30, 49] = new(BlockShape.Empty, false, true);
    Check(!GroundSpawnRules.SheepPosition(world, 30, 50, out _));
});
Run("Sheep eggs support half blocks and platforms but reject world edges and partial exploration", () => {
    var world = new TestWorld(); world[30, 50] = new(BlockShape.HalfBlock, false, false);
    Check(GroundSpawnRules.SheepPosition(world, 30, 50, out var center));
    Check(Math.Abs(center.Y + GroundSpawnRules.SheepSize.Y / 2 - 50.5f) < 0.001f);
    world[30, 50] = new(BlockShape.Platform, false, false);
    Check(GroundSpawnRules.SheepPosition(world, 30, 50, out _));
    Check(!GroundSpawnRules.SheepPosition(world, 1, 50, out _));
    var fog = new ExplorationMask(100, 100, (x, _) => x >= 30);
    Check(!GroundSpawnRules.SheepPosition(world, 30, 50, out _, fog));
});
Run("An unexplored spawn target never reads the hidden terrain", () => {
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    Check(!GroundSpawnRules.SheepPosition(new HiddenSpawnWorld(), 30, 50, out _, fog));
});
Run("Storage block faces are opaque logical 16px tiles with distinct material palettes", () => {
    byte[] iron = ContentPixelArt.BlockFace(0xCDD0D4, false), diamond = ContentPixelArt.BlockFace(0x6DE6E9, true);
    Check(iron.Length == 16 * 16 * 4 && !iron.SequenceEqual(diamond));
    for (int i = 3; i < iron.Length; i += 4) Check(iron[i] == 255 && diamond[i] == 255);
});
Run("Cube and sheep egg icons preserve transparent silhouettes at native item sizes", () => {
    byte[] cube = ContentPixelArt.Cube(0xF6CF45, false), egg = ContentPixelArt.SheepEgg();
    Check(cube.Length == 24 * 24 * 4 && egg.Length == 16 * 24 * 4);
    Check(cube.Where((_, i) => i % 4 == 3).Contains((byte)0) && cube.Where((_, i) => i % 4 == 3).Contains((byte)255));
    Check(egg.Where((_, i) => i % 4 == 3).Contains((byte)0) && egg.Where((_, i) => i % 4 == 3).Contains((byte)255));
});
Run("Slime egg has the existing egg silhouette with a distinct blue palette", () => {
    byte[] sheep = ContentPixelArt.SheepEgg(), slime = ContentPixelArt.SlimeEgg();
    Check(slime.Length == 16 * 24 * 4 && !slime.SequenceEqual(sheep));
    for (int i = 0; i < slime.Length; i += 4) {
        Check(slime[i + 3] == sheep[i + 3]);
        if (slime[i + 3] != 0) Check(slime[i + 2] > slime[i] && slime[i + 2] > slime[i + 1]);
    }
});
Run("Cube icons have connected faces and a solid center down to the bottom vertex", () => {
    foreach (bool gem in new[] { false, true })
    {
        byte[] cube = ContentPixelArt.Cube(0x6DE6E9, gem);
        bool Opaque(int x, int y) => cube[(x + y * 24) * 4 + 3] == 255;
        for (int y = 2; y < 22; y++)
        {
            int[] filled = Enumerable.Range(0, 24).Where(x => Opaque(x, y)).ToArray();
            Check(filled.Length > 0 && filled.Length == filled[^1] - filled[0] + 1);
            Check(Opaque(11, y) && Opaque(12, y));
        }
        Check(!Opaque(2, 2) && !Opaque(21, 21) && Opaque(2, 8) && Opaque(21, 16));
        Check(cube[(5 + 12 * 24) * 4] > cube[(18 + 12 * 24) * 4]);
    }
});
Run("Catalog search handles Chinese, internal English names, multiple terms and numeric IDs", () => {
    var catalog = new ItemCatalog(new CatalogEntry[] { new(1, "铁镐", "IronPickaxe"), new(11, "铁矿", "IronOre"), new(2, "泥土块", "DirtBlock") });
    catalog.Search("铁"); Check(catalog.Count == 2);
    catalog.Search("  iRoN  pick "); Check(catalog.Count == 1 && catalog.TryVisible(0, out var entry) && entry.Type == 1);
    catalog.Search("11"); Check(catalog.Count == 1 && catalog.TryVisible(0, out entry) && entry.Type == 11);
    catalog.Search("missing"); Check(catalog.Count == 0 && catalog.Pages == 1 && catalog.VisibleCount == 0 && !catalog.TryVisible(0, out _));
    catalog.Search(" \t "); Check(catalog.Count == 3);
});
Run("Catalog pagination visits every item once and never grants an empty or stale slot", () => {
    var catalog = new ItemCatalog(Enumerable.Range(1, 5456).Select(id => new CatalogEntry(id, "物品" + id, "Item" + id)));
    var seen = new HashSet<int>();
    for (int page = 0; page < catalog.Pages; page++)
    {
        Check(catalog.Page == page && catalog.VisibleCount <= 30 && !catalog.TryVisible(-1, out _) && !catalog.TryVisible(catalog.VisibleCount, out _));
        for (int slot = 0; slot < catalog.VisibleCount; slot++)
        {
            Check(catalog.TryVisible(slot, out var entry) && seen.Add(entry.Type));
        }
        catalog.MovePage(1);
    }
    Check(seen.Count == 5456 && catalog.VisibleCount == 26);
    catalog.MovePage(int.MaxValue); Check(catalog.Page == catalog.Pages - 1);
    catalog.Search("5456"); Check(catalog.Page == 0 && catalog.Count == 1 && catalog.TryVisible(0, out var last) && last.Type == 5456);
    catalog.MovePage(int.MinValue); Check(catalog.Page == 0);
    catalog.Search("none"); catalog.MovePage(1); Check(catalog.Page == 0 && !catalog.TryVisible(0, out _));
});
Run("Runtime texture data uses the engine's versioned RawImg format and exact payload size", () => {
    byte[] pixels = ContentPixelArt.SheepEgg(), encoded = ContentPixelArt.RawImage(16, 24, pixels);
    using var stream = new MemoryStream(encoded); using var reader = new BinaryReader(stream);
    Check(reader.ReadInt32() == 1 && reader.ReadInt32() == 16 && reader.ReadInt32() == 24);
    Check(reader.ReadBytes(pixels.Length).SequenceEqual(pixels) && stream.Position == stream.Length);
    bool rejected = false; try { ContentPixelArt.RawImage(16, 24, new byte[4]); } catch (ArgumentException) { rejected = true; }
    Check(rejected);
});
Run("Zenith blades reach their aimed point in real depth and return beside the launch position", () => {
    Vector3 start = new(30, 30, 32), target = new(30, 30, 0.6f);
    var blade = new SwordFlight(SwordEffect.Zenith, 1, start, target, Vector3.UnitX, 60, 1);
    blade.Update(0.325f); Check(Vector3.Distance(blade.Position, target) < 0.001f);
    blade.Update(0.325f); Check(Vector3.Distance(blade.Position, blade.ReturnPoint) < 0.001f && !blade.Active);
    Check(Math.Abs(blade.Position.X-start.X)>3 && blade.Position.Z<start.Z-2);
});
Run("Zenith range reaches 96 tiles and return corridors alternate left and right", () => {
    Vector3 start=new(30,30,120);
    for(int variant=0;variant<2;variant++) {
        var blade=new SwordFlight(SwordEffect.Zenith,1,start,start-Vector3.UnitZ*200,Vector3.UnitX,60,variant);
        blade.Update(0.45f); Check(Vector3.Distance(blade.Position,start-Vector3.UnitZ*96)<0.001f);
        blade.Update(0.45f); Check(!blade.Active && Vector3.Distance(blade.Position,blade.ReturnPoint)<0.001f);
        Check(Math.Sign(blade.Position.X-start.X)==(variant==0?1:-1));
    }
});
Run("Zenith return cannot converge onto the camera center even for a short target", () => {
    for(int variant=0;variant<6;variant++) {
        Vector3 start=new(30,30,32);
        var blade=new SwordFlight(SwordEffect.Zenith,1,start,start-Vector3.UnitZ*2,Vector3.UnitX,60,variant);
        for(int frame=0;frame<39;frame++) {
            blade.Update(1f/60);
            if(blade.Age>0.55f) Check(Math.Abs(blade.Position.X-start.X)>2.5f && SwordVisuals.CameraFade(blade.Effect,blade.Position,start)<0.5f);
        }
    }
});
Run("Sword visuals warm Flying Dragon's grayscale texture and fade camera-near Zenith blades", () => {
    Check(SwordVisuals.DragonCore.X>SwordVisuals.DragonCore.Y && SwordVisuals.DragonCore.Y>SwordVisuals.DragonCore.Z*2);
    Check(SwordVisuals.DragonGlow.X>SwordVisuals.DragonGlow.Y*2);
    Check(SwordVisuals.CameraFade(SwordEffect.Zenith,Vector3.Zero,Vector3.Zero)==0);
    Check(SwordVisuals.CameraFade(SwordEffect.Zenith,Vector3.UnitZ*5,Vector3.Zero)==0.5f);
    Check(SwordVisuals.CameraFade(SwordEffect.Zenith,Vector3.UnitZ*7,Vector3.Zero)==1);
    Check(SwordVisuals.CameraFade(SwordEffect.FlyingDragon,Vector3.Zero,Vector3.Zero)==1);
});
Run("Moving sword attacks do not apply instant damage to a distant sheet enemy", () => {
    var blade = new SwordFlight(SwordEffect.Zenith, 1, new(30, 30, 32), new(30, 30, 0.57f), Vector3.UnitX, 60, 0);
    CombatBody[] enemies = { new(1, new(30, 30), new(2, 3), true) };
    blade.Update(1f / 60); Check(!blade.Sweep(enemies, 128, null).Any());
    int hits = 0;
    for (int frame = 1; frame < 45; frame++)
    {
        blade.Update(1f / 60);
        foreach (var hit in blade.Sweep(enemies, 128, null)) { blade.MarkHit(hit.Id); hits++; }
    }
    Check(hits == 1);
});
Run("Zenith curves remain finite for vertical and oblique aim, with bounded trail storage", () => {
    foreach (Vector3 direction in new[] { Vector3.UnitY, -Vector3.UnitY, Vector3.Normalize(new Vector3(1, 2, -3)) })
    for (int variant = 0; variant < 6; variant++)
    {
        var blade = new SwordFlight(SwordEffect.Zenith, 1, new(30, 30, 32), new Vector3(30, 30, 32) + direction * 40, Vector3.UnitY, 60, variant);
        for (int frame = 0; frame < 60; frame++)
        { blade.Update(1f / 60); Check(float.IsFinite(blade.Position.LengthSquared()) && blade.Trail.Count() <= SwordFlight.TrailLength); }
    }
});
Run("Flying Dragon wave travels at projectile speed and never exceeds the 48-tile reach", () => {
    var blade = new SwordFlight(SwordEffect.FlyingDragon, 1, new(30, 30, 32), new(130, 30, 32), Vector3.UnitX, 60, 0);
    blade.Update(0.1f); Check(Math.Abs(blade.Position.X - 36) < 0.001f && blade.Active);
    blade.Update(1); Check(Math.Abs(blade.Position.X - 78) < 0.001f && !blade.Active);
});
Run("Fast sword waves use swept collision instead of tunneling through thin enemies", () => {
    var blade = new SwordFlight(SwordEffect.FlyingDragon, 1, new(30, 30, 10), new(30, 30, -10), Vector3.UnitX, 120, 0);
    blade.Update(0.1f);
    var hit = blade.Sweep(new[] { new CombatBody(7, new(30, 30), new(1, 2), true) }, 12, null).Single();
    Check(hit.Id == 7 && hit.Distance > 0 && hit.Distance < 12);
    blade.MarkHit(7); Check(!blade.Sweep(new[] { new CombatBody(7, new(30, 30), new(1, 2), true) }, 12, null).Any());
});
Run("Sword waves retain critter eligibility, exploration and actual sheet depth restrictions", () => {
    var blade = new SwordFlight(SwordEffect.FlyingDragon, 1, new(30, 30, 10), new(30, 30, -10), Vector3.UnitX, 120, 0);
    blade.Update(0.1f);
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    var enemy = new CombatBody(1, new(30, 30), new(2, 3), true);
    Check(!blade.Sweep(new[] { enemy }, 12, fog).Any());
    Check(!blade.Sweep(new[] { enemy with { Eligible = false } }, 12, null).Any());
    Check(!blade.Sweep(new[] { enemy with { Center = new(50, 30) } }, 12, null).Any());
});
Run("Wide waves cannot hit around opaque terrain or an unexplored volume", () => {
    var world = new TestInteractionWorld(); world[30, 30] = new(true, false, BlockShape.Solid);
    var blade = new SwordFlight(SwordEffect.FlyingDragon, 1, new(30.5f, 30.5f, 3), new(30.5f, 30.5f, -5), Vector3.UnitX, 60, 0);
    blade.Update(0.05f);
    var enemy = new CombatBody(1, new(30.5f, 30.5f), new(1, 2), true, true);
    Check(!blade.Sweep(new[] { enemy }, 3, null, world).Any());
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    world.RejectHiddenReads = true;
    Check(!blade.Sweep(new[] { enemy }, 3, fog, world).Any());
});
Run("A Flying Dragon wave can hit five distinct enemies and then expires", () => {
    var blade = new SwordFlight(SwordEffect.FlyingDragon, 1, new(30, 30, 32), new(30, 30, 0), Vector3.UnitX, 60, 0);
    for (int id = 0; id < 4; id++) { blade.MarkHit(id); Check(blade.Active && blade.CanHitMore); }
    blade.MarkHit(4); Check(!blade.Active && !blade.CanHitMore);
    Vector3 previous = blade.Position; blade.Update(1); Check(blade.Position == previous);
});
Run("Stopped sword flights do not move and invalid flight inputs cannot poison rendering", () => {
    var blade = new SwordFlight(SwordEffect.Zenith, 1, new(30, 30, 32), new(30, 30, 0), Vector3.UnitX, 60, 0);
    blade.Stop(); blade.Update(0.1f); Check(blade.Position == new Vector3(30, 30, 32));
    bool rejected = false;
    try { _ = new SwordFlight(SwordEffect.Zenith, 1, new(float.NaN, 0, 0), Vector3.One, Vector3.UnitX, 60, 0); }
    catch (ArgumentException) { rejected = true; }
    Check(rejected);
});
Run("A neutral iron golem never attacks an unprovoking nearby player", () => {
    var brain = new IronGolemBrain();
    GolemTarget[] nearby = { new(0, true, new(30, 0), new(20, 42), true, true) };
    for (int frame = 0; frame < 600; frame++) Check(brain.Step(Vector2.Zero, 0, true, true, true, nearby).Strike is null);
    Check(brain.AngryPlayer == -1 && IronGolemBrain.Height > 42);
});
Run("Iron golems choose the nearest visible eligible hostile and telegraph one uppercut", () => {
    var brain = new IronGolemBrain();
    GolemTarget[] targets = { new(1, false, new(55, 0), new(20, 30), true, true),
        new(2, false, new(30, 0), new(20, 30), true, true), new(3, false, new(10, 0), new(20, 30), false, true) };
    int strikes = 0; bool anticipation = false, uppercut = false;
    for (int frame = 0; frame < IronGolemBrain.AttackTicks; frame++)
    {
        var motion = brain.Step(Vector2.Zero, 0, true, true, true, targets);
        anticipation |= motion.Frame == 5; uppercut |= motion.Frame == 6;
        if (motion.Strike is { } hit) { strikes++; Check(hit.Id == 2 && !hit.Player); }
    }
    Check(strikes == 1 && anticipation && uppercut);
});
Run("Nearby monsters take priority and permanently clear a previously provoked player grudge", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget[] targets = { new(3, false, new(20, 0), new(20, 30), true, true), new(0, true, new(-40, 0), new(20, 42), true, true) };
    GolemTarget? strike = null;
    for (int frame = 0; frame < IronGolemBrain.WindupTicks; frame++)
    {
        var motion = brain.Step(Vector2.Zero, 0, true, true, true, targets); Check(motion.Facing == 1);
        strike ??= motion.Strike;
    }
    Check(strike is { Player: false, Id: 3 } && brain.AngryPlayer == -1 && brain.AngerTicks == 0);
    for (int frame = 0; frame < 100; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { targets[1] }).Strike is null);
});
Run("Repeated player hits during a monster windup neither provoke nor reset the enemy uppercut", () => {
    var brain = new IronGolemBrain();
    GolemTarget[] targets = { new(3, false, new(-30, 0), new(20, 30), true, true), new(0, true, new(40, 0), new(20, 42), true, true) };
    int strikes = 0;
    for (int frame = 0; frame < IronGolemBrain.AttackTicks; frame++)
    {
        brain.Retaliate(0, true);
        var motion = brain.Step(Vector2.Zero, 0, true, true, true, targets);
        if (motion.Strike is { } hit) { Check(!hit.Player && hit.Id == 3); strikes++; }
        Check(brain.AngryPlayer == -1);
    }
    Check(strikes == 1);
});
Run("A monster entering on the player impact frame cancels that hit and starts a fresh enemy windup", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget player = new(0, true, new(-30, 0), new(20, 42), true, true);
    GolemTarget enemy = new(0, false, new(30, 0), new(20, 30), true, true);
    for (int frame = 0; frame < IronGolemBrain.WindupTicks - 1; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player }).Strike is null);
    var canceled = brain.Step(Vector2.Zero, 0, true, true, true, new[] { player, enemy });
    Check(canceled.Strike is null && canceled.Frame == 5 && canceled.Facing == 1 && brain.AngryPlayer == -1);
    for (int frame = 0; frame < IronGolemBrain.WindupTicks - 2; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player, enemy }).Strike is null);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player, enemy }).Strike is { Player: false, Id: 0 });
});
Run("Nearby occluded monsters suppress grudges without allowing attacks through walls", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget player = new(0, true, new(30, 0), new(20, 42), true, true);
    GolemTarget enemy = new(2, false, new(-30, 0), new(20, 30), true, false);
    for (int frame = 0; frame < 100; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player, enemy }).Strike is null);
    Check(brain.AngryPlayer == -1);
    for (int frame = 0; frame < 100; frame++) Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player }).Strike is null);
});
Run("A monster exactly at the 20-tile boundary clears player hostility", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget[] targets = { new(2, false, new(IronGolemBrain.EnemyRange, 0), new(20, 30), true, false),
        new(0, true, new(30, 0), new(20, 42), true, true) };
    Check(brain.Step(Vector2.Zero, 0, true, true, true, targets).Strike is null && brain.AngryPlayer == -1);
});
Run("Out-of-range and ineligible monsters do not prevent deliberate retaliation", () => {
    foreach (GolemTarget enemy in new[] { new GolemTarget(2, false, new(IronGolemBrain.EnemyRange + 1, 0), new(20, 30), true, true),
        new GolemTarget(2, false, new(20, 0), new(20, 30), false, true) })
    {
        var brain = new IronGolemBrain(); brain.Retaliate(0, false);
        GolemTarget player = new(0, true, new(-30, 0), new(20, 42), true, true);
        GolemTarget? hit = null;
        for (int frame = 0; frame < IronGolemBrain.WindupTicks; frame++)
            hit ??= brain.Step(Vector2.Zero, 0, true, true, true, new[] { enemy, player }).Strike;
        Check(hit is { Player: true, Id: 0 });
    }
});
Run("An ignored combat hit cannot reappear later, but a fresh peacetime hit still provokes", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, true);
    GolemTarget player = new(0, true, new(30, 0), new(20, 42), true, true);
    for (int frame = 0; frame < 100; frame++) Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player }).Strike is null);
    brain.Retaliate(0, false);
    GolemTarget? hit = null;
    for (int frame = 0; frame < IronGolemBrain.AttackTicks; frame++)
        hit ??= brain.Step(Vector2.Zero, 0, true, true, true, new[] { player }).Strike;
    Check(hit is { Player: true, Id: 0 });
});
Run("Iron golem strikes miss when the locked target leaves reach during the windup", () => {
    var brain = new IronGolemBrain();
    GolemTarget enemy = new(2, false, new(30, 0), new(20, 30), true, true);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { enemy }).Frame == 5);
    for (int frame = 0; frame < 20; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { enemy with { Center = new(500, 0) } }).Strike is null);
});
Run("A detached or dead aggressor is unavailable even during the uppercut impact frame", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget player = new(0, true, new(30, 0), new(20, 42), true, true);
    brain.Step(Vector2.Zero, 0, true, true, true, new[] { player });
    for (int frame = 0; frame < 20; frame++)
        Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player with { Eligible = false } }).Strike is null);
});
Run("Iron golem grudges expire and returning across the map cannot restore pursuit", () => {
    var brain = new IronGolemBrain(); brain.Retaliate(0, false);
    GolemTarget player = new(0, true, new(600, 0), new(20, 42), true, true);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player }).VelocityX > 0);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { player with { Center = new(900, 0) } }).VelocityX > 0);
    brain.Step(Vector2.Zero, 0, true, true, true, new[] { player with { Center = new(1100, 0) } }); Check(brain.AngryPlayer == -1);
    brain.Retaliate(0, false);
    for (int frame = 0; frame < IronGolemBrain.AngerDuration; frame++) brain.Step(Vector2.Zero, 0, true, true, true, Array.Empty<GolemTarget>());
    Check(brain.AngryPlayer == -1);
});
Run("Golems ignore unseen or distant enemies and stop at steps instead of jumping", () => {
    var brain = new IronGolemBrain();
    GolemTarget target = new(1, false, new(200, 0), new(20, 30), true, true);
    Check(brain.Step(Vector2.Zero, 0, true, true, false, new[] { target }).VelocityX == 0);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { target with { Visible = false } }).VelocityX == 0);
    Check(brain.Step(Vector2.Zero, 0, true, true, true, new[] { target with { Center = new(400, 0) } }).VelocityX == 0);
    Check(!IronGolemBrain.CanReach(Vector2.Zero, target with { Center = new(0, -150) }));
});
Run("Iron golem uppercuts lift targets but preserve boss knockback immunity", () => {
    Vector2 velocity = new(1, 2);
    Check(IronGolemBrain.LaunchVelocity(velocity, 1, 1) == new Vector2(4, -8));
    Check(IronGolemBrain.LaunchVelocity(velocity, 0.5f, -1) == new Vector2(-2, -4));
    Check(IronGolemBrain.LaunchVelocity(velocity, 0, 1) == velocity);
    Check(IronGolemBrain.LaunchVelocity(new(0, -12), 1, 1).Y == -12);
});
Run("Tall iron golem spawning checks headroom and cannot reuse sheep-sized openings", () => {
    var world = new TestWorld(); world[30, 50] = new(BlockShape.Solid);
    Check(GroundSpawnRules.IronGolemPosition(world, 30, 50, out var center));
    Check(Math.Abs(center.Y + IronGolemBrain.Height / 32f - 50) < 0.001f);
    world[30, 45] = new(BlockShape.Solid);
    Check(!GroundSpawnRules.IronGolemPosition(world, 30, 50, out _));
    Check(GroundSpawnRules.SheepPosition(world, 30, 50, out _));
});
Run("Iron golem spawning rejects liquid, world borders and unexplored ground", () => {
    var world = new TestWorld(); world[30, 50] = new(BlockShape.Solid); world[30, 47] = new(BlockShape.Empty, false, true);
    Check(!GroundSpawnRules.IronGolemPosition(world, 30, 50, out _));
    Check(!GroundSpawnRules.IronGolemPosition(world, 1, 50, out _));
    var fog = new ExplorationMask(100, 100, (_, _) => false);
    Check(!GroundSpawnRules.IronGolemPosition(new HiddenSpawnWorld(), 30, 50, out _, fog));
});
Run("A pumpkin on a four-block iron T creates exactly one golem at the original base", () => {
    var world = new SummonWorld();
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.Summoned);
    Check(world.Spawns == 1 && world.Consumed.Count == 5);
    Check(world.SpawnCenter.X == 30.5f && world.SpawnCenter.Y + IronGolemBrain.Height / 32f == 53);
    Check(world.GetCell(30, 53).Shape == BlockShape.Solid);
    Check(world.GetCell(30, 52).Shape == BlockShape.Empty);
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NotPattern && world.Spawns == 1);
});
Run("Every iron arm and base is required; partial structures keep their materials", () => {
    foreach (var cell in new[] { new StructureTile(29, 51), new StructureTile(30, 51), new StructureTile(31, 51), new StructureTile(30, 52) })
    {
        var world = new SummonWorld(); world.Structures.Remove(cell);
        Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NotPattern);
        Check(world.Spawns == 0 && world.Consumed.Count == 0);
    }
});
Run("Ordinary ores, other metal blocks and pumpkin plants cannot substitute recipe cells", () => {
    var world = new SummonWorld(); world.Structures[new(29, 51)] = GolemStructureCell.Other;
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NotPattern);
    world = new SummonWorld(); world.Structures[new(30, 50)] = GolemStructureCell.Other;
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NotPattern);
    Check(world.Spawns == 0 && world.Consumed.Count == 0);
});
Run("Only the newly placed head position is considered; a misplaced pumpkin cannot trigger a nearby T", () => {
    var world = new SummonWorld();
    Check(GolemSummonRules.TrySummon(world, 29, 50) == GolemSummonResult.NotPattern);
    Check(GolemSummonRules.TrySummon(world, 30, 49) == GolemSummonResult.NotPattern);
    Check(world.Spawns == 0 && world.Consumed.Count == 0);
});
Run("A golem requires full headroom above the pumpkin, not just empty recipe cells", () => {
    var world = new SummonWorld(); world.Terrain[new(30, 48)] = new(BlockShape.Solid);
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NoSpace);
    Check(world.Spawns == 0 && world.Consumed.Count == 0);
});
Run("Extra iron below the arms obstructs the body and cannot be silently consumed", () => {
    var world = new SummonWorld(); world.Terrain[new(29, 52)] = new(BlockShape.Solid);
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NoSpace);
    Check(world.Spawns == 0 && world.Consumed.Count == 0);
});
Run("Liquids in recipe cells still obstruct summoning after virtual structure removal", () => {
    var world = new SummonWorld(); world.Terrain[new(30, 50)] = new(BlockShape.Solid, false, true);
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NoSpace);
    Check(world.Spawns == 0 && world.Consumed.Count == 0);
});
Run("Background walls survive summoning and do not obstruct the golem", () => {
    var world = new SummonWorld(); world.Terrain[new(30, 50)] = new(BlockShape.Solid, true);
    world.Terrain[new(30, 49)] = new(BlockShape.Empty, true);
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.Summoned);
    Check(world.GetCell(30, 50).HasWall && world.GetCell(30, 49).HasWall);
});
Run("Every consumed tile is checked for protection before spawning or removing anything", () => {
    foreach (var cell in new[] { new StructureTile(30, 50), new StructureTile(29, 51), new StructureTile(30, 51), new StructureTile(31, 51), new StructureTile(30, 52) })
    {
        var world = new SummonWorld { Protected = cell };
        Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.Protected);
        Check(world.Spawns == 0 && world.Consumed.Count == 0);
    }
});
Run("A full NPC array cannot consume the structure or produce partial material refunds", () => {
    var world = new SummonWorld { FullNpcs = true };
    Check(GolemSummonRules.TrySummon(world, 30, 50) == GolemSummonResult.NoNpcSlot);
    Check(world.Spawns == 0 && world.Consumed.Count == 0 && world.Structures.Count == 5);
});
Run("World-edge and extreme structure coordinates are rejected without reading tiles", () => {
    var world = new SummonWorld { RejectReads = true };
    foreach (var point in new[] { new StructureTile(0, 50), new StructureTile(30, 0), new StructureTile(99, 50),
        new StructureTile(30, 99), new StructureTile(int.MaxValue, 50), new StructureTile(30, int.MinValue) })
        Check(GolemSummonRules.TrySummon(world, point.X, point.Y) == GolemSummonResult.NotPattern);
});
Run("Textured block icons keep independent top/front/side faces and closed cube seams", () => {
    byte[] Solid(int r, int g, int b) { var pixels = new byte[16 * 16 * 4]; for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = (byte)r; pixels[i + 1] = (byte)g; pixels[i + 2] = (byte)b; pixels[i + 3] = 255; } return pixels; }
    byte[] icon = ContentPixelArt.TexturedCube(Solid(200, 0, 0), Solid(0, 200, 0), Solid(0, 0, 200));
    Check(icon[(12 + 4 * 24) * 4] == 200 && icon[(6 + 12 * 24) * 4 + 1] > 0 && icon[(17 + 12 * 24) * 4 + 2] > 0);
    for (int y = 2; y < 22; y++)
    {
        int[] opaque = Enumerable.Range(0, 24).Where(x => icon[(x + y * 24) * 4 + 3] > 0).ToArray();
        Check(opaque.Length > 0 && opaque[^1] - opaque[0] + 1 == opaque.Length);
    }
    Check(icon[3] == 0 && icon[(23 + 23 * 24) * 4 + 3] == 0);
});
Run("Cube projection samples the original texture detail rather than replacing faces with a flat color", () => {
    byte[] face = new byte[16 * 16 * 4];
    for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { int i = (x + y * 16) * 4; face[i] = (byte)(x * 16); face[i + 1] = (byte)(y * 16); face[i + 3] = 255; }
    byte[] icon = ContentPixelArt.TexturedCube(face, face, face);
    Check(icon.Where((_, i) => i % 4 == 0).Distinct().Count() > 10);
    Check(icon.Where((_, i) => i % 4 == 1).Distinct().Count() > 10);
});
Run("Creature thumbnails preserve aspect ratio, source alpha and transparent margins", () => {
    byte[] source = new byte[20 * 40 * 4];
    for (int i = 0; i < source.Length; i += 4) { source[i] = 123; source[i + 3] = 200; }
    byte[] icon = ContentPixelArt.Thumbnail(20, 40, source, 32, 40);
    int occupied = Enumerable.Range(0, 32 * 40).Count(i => icon[i * 4 + 3] == 200);
    Check(occupied == 19 * 38 && icon[(16 + 20 * 32) * 4] == 123);
    Check(icon.Take(32 * 4).All(b => b == 0) && icon.Skip(39 * 32 * 4).All(b => b == 0));
});
Run("Malformed face and thumbnail inputs are rejected before allocation or sampling", () => {
    int rejected = 0;
    try { ContentPixelArt.TexturedCube(new byte[1], new byte[1], new byte[1]); } catch (ArgumentException) { rejected++; }
    try { ContentPixelArt.Thumbnail(0, 1, Array.Empty<byte>(), 32, 40); } catch (ArgumentException) { rejected++; }
    try { ContentPixelArt.Thumbnail(10, 10, new byte[400], 2, 40); } catch (ArgumentException) { rejected++; }
    Check(rejected == 3);
});
Run("Boat footprints accept replaceable plants but reject solid blocks and furniture", () => {
    var world = new BoatWorld(); world.Floor(new(true, BlockShape.Solid));
    Check(BoatRules.TryPlacement(world, 30, 50, out var p) && p.X == 29 && p.Y == 48 && p.SurfaceY == 50);
    world[31, 48] = new(true, BlockShape.Empty);
    Check(!BoatRules.TryPlacement(world, 30, 50, out _));
    world[31, 48] = new(true, BlockShape.Empty, Replaceable: true);
    Check(BoatRules.TryPlacement(world, 30, 50, out _));
    world[31, 48] = new(true, BlockShape.Solid, Replaceable: true);
    Check(!BoatRules.TryPlacement(world, 30, 50, out _));
});
Run("Boats can overhang either edge or rest on a single center support", () => {
    for (int x = 29; x <= 31; x++) {
        var world = new BoatWorld(); world[x, 50] = new(true, BlockShape.Solid);
        Check(BoatRules.TryPlacement(world, 30, 50, out var p) && p.SurfaceY == 50);
    }
});
Run("Boats float at the actual surface of all four liquid types", () => {
    for (int kind = 0; kind < 4; kind++) {
        var world = new BoatWorld(); world.Floor(new(false, BlockShape.Empty, 128, kind));
        Check(BoatRules.TryPlacement(world, 30, 50, out var p));
        Check(MathF.Abs(p.SurfaceY - (51 - 128 / 255f)) < .001f && p.SeatBottom.Y == p.SurfaceY - .75f);
    }
});
Run("Shorelines and uneven liquid support keep boats above the highest surface", () => {
    var world = new BoatWorld(); world.Floor(new(false, BlockShape.Empty, 255));
    world[29, 50] = new(true, BlockShape.Solid); Check(BoatRules.TryPlacement(world, 30, 50, out var p) && p.SurfaceY == 50);
    world[29, 50] = new(false, BlockShape.Empty, 255, 1); Check(BoatRules.TryPlacement(world, 30, 50, out p) && p.SurfaceY == 50);
    world[29, 50] = new(false, BlockShape.Empty, 1); Check(BoatRules.TryPlacement(world, 30, 50, out p) && p.SurfaceY == 50);
    world[30, 50] = world[31, 50] = default;
    Check(BoatRules.TryPlacement(world, 30, 50, out p) && MathF.Abs(p.SurfaceY - (51 - 1 / 255f)) < .001f);
});
Run("Fully unsupported and submerged boat bodies are rejected", () => {
    var world = new BoatWorld();
    Check(!BoatRules.TryPlacement(world, 30, 50, out _));
    world[29, 50] = new(true, BlockShape.Empty, Replaceable: true);
    Check(!BoatRules.TryPlacement(world, 30, 50, out _));
    world[29, 50] = new(true, BlockShape.Solid); world[30, 49] = new(false, BlockShape.Empty, 1);
    Check(!BoatRules.TryPlacement(world, 30, 50, out _));
});
Run("Half-block support raises the boat by exactly its surface offset", () => {
    var world = new BoatWorld(); world.Floor(new(true, BlockShape.HalfBlock));
    Check(BoatRules.TryPlacement(world, 30, 50, out var p) && p.SurfaceY == 50.5f);
    world[29, 50] = new(true, BlockShape.Solid); Check(BoatRules.TryPlacement(world, 30, 50, out p) && p.SurfaceY == 50);
});
Run("World-edge and extreme boat positions fail without reading terrain", () => {
    var world = new BoatWorld { RejectReads = true };
    foreach (int x in new[] { int.MinValue, -1, 0, 12, 87, int.MaxValue })
        Check(!BoatRules.TryPlacement(world, x, 50, out _));
    foreach (int y in new[] { int.MinValue, 0, 13, 88, int.MaxValue })
        Check(!BoatRules.TryPlacement(world, 30, y, out _));
});
Run("Boat placement checks the complete exploration footprint before reading terrain", () => {
    var world = new BoatWorld { RejectReads = true };
    var fog = new ExplorationMask(100, 100, (x, y) => x == 30 && y == 50);
    Check(!BoatRules.TryPlacement(world, 30, 50, out _, fog));
});
Run("3D boat placement follows the crosshair, range and exploration", () => {
    var world = new BoatWorld(); world.Floor(new(true, BlockShape.Solid));
    Check(BoatRules.TryAim(world, new(30.5f, 49, 4), -Vector3.UnitZ, 6, out var p) && p.X == 29);
    Check(!BoatRules.TryAim(world, new(30.5f, 49, 10), -Vector3.UnitZ, 6, out _));
    Check(!BoatRules.TryAim(world, new(30.5f, 49, 4), Vector3.UnitZ, 6, out _));
    Check(!BoatRules.TryAim(world, new(float.NaN, 49, 4), -Vector3.UnitZ, 6, out _));
    Check(!BoatRules.TryAim(world, new(30.5f, 49, 4), -Vector3.UnitZ, 6, out _, new ExplorationMask(100, 100, (_, _) => false)));
});
Run("Boat passengers include iron-golem-sized bodies but exclude bosses, segmented and protected creatures", () => {
    Check(BoatRules.FitsPassenger(true, false, false, false, new(32, 24)));
    Check(!BoatRules.FitsPassenger(true, true, false, false, new(32, 24)));
    Check(!BoatRules.FitsPassenger(true, false, true, false, new(32, 24)));
    Check(!BoatRules.FitsPassenger(true, false, false, true, new(32, 24)));
    Check(BoatRules.FitsPassenger(true, false, false, false, new(44, 76)));
    Check(BoatRules.FitsPassenger(true, false, false, false, new(200, 300)));
    Check(!BoatRules.FitsPassenger(true, false, false, false, new(0, 76)));
    Check(!BoatRules.FitsPassenger(true, false, false, false, new(44, float.PositiveInfinity)));
    Check(!BoatRules.FitsPassenger(false, false, false, false, new(32, 24)));
    Check(!BoatRules.FitsPassenger(true, false, false, false, new(float.NaN, 24)));
});
Run("Fast projectiles cannot tunnel through a fragile boat", () => {
    Check(BoatRules.SweptHit(new(0, 25), new(100, 25), new(2), new(40, 20), new(88, 52)));
    Check(!BoatRules.SweptHit(new(0, 5), new(100, 5), new(2), new(40, 20), new(88, 52)));
    Check(BoatRules.SweptHit(new(42, 25), new(42, 25), new(2), new(40, 20), new(88, 52)));
    Check(!BoatRules.SweptHit(new(float.NaN, 5), new(100, 5), new(2), new(40, 20), new(88, 52)));
});
Run("Liquid geometry covers full, partial and half-block cells without solid blocks", () => {
    Check(LiquidRules.Bounds(BlockShape.Empty, 255, out var top, out var bottom) && top == 0 && bottom == 1);
    Check(LiquidRules.Bounds(BlockShape.Empty, 128, out top, out bottom) && MathF.Abs(top - 127 / 255f) < .001f && bottom == 1);
    Check(LiquidRules.Bounds(BlockShape.HalfBlock, 255, out top, out bottom) && top == 0 && bottom == .5f);
    Check(!LiquidRules.Bounds(BlockShape.Solid, 255, out _, out _));
    Check(!LiquidRules.Bounds(BlockShape.Empty, 0, out _, out _));
});
Run("Water, lava, honey and shimmer have distinct visible translucent colors", () => {
    var colors = Enumerable.Range(0, 4).Select(LiquidRules.Color).ToArray();
    Check(colors.Distinct().Count() == 4 && colors.All(c => c.W > .5f && c.W < 1));
});
Run("Boat tile atlas retains all six frame crops with transparent padding", () => {
    byte[] source = new byte[16 * 16 * 4];
    for (int i = 0; i < 256; i++) { source[i * 4] = (byte)i; source[i * 4 + 3] = 255; }
    byte[] atlas = ContentPixelArt.BoatTileAtlas(source);
    Check(atlas.Length == 54 * 36 * 4);
    for (int y = 0; y < 36; y++) for (int x = 0; x < 54; x++)
        Check(atlas[(x + y * 54) * 4 + 3] == (x % 18 >= 16 || y % 18 >= 16 ? 0 : 255));
});
PistonCoreChecks.Run(Run);
HopperCoreChecks.Run(Run);
VillagerCoreChecks.Run(Run);
VillageGolemSpawnChecks.Run(Run);
Run("Crouch support preserves platform drop-through and accepts solid or half-block ground", () => {
    var world = new TestWorld(); world[30,50]=new(BlockShape.Solid);
    Check(CrouchRules.Support(world,30,50,1.25f) is { Ground:true,DropThrough:false });
    world[30,50]=new(BlockShape.Platform);
    Check(CrouchRules.Support(world,30,50,1.25f) is { Ground:true,DropThrough:true });
    world[31,50]=new(BlockShape.Solid);
    Check(!CrouchRules.Support(world,30,50,1.25f).DropThrough);
    world[30,50]=new(BlockShape.HalfBlock); world[31,50]=default;
    Check(!CrouchRules.Support(world,30,50,1.25f).Ground);
    Check(CrouchRules.Support(world,30,50.5f,1.25f).Ground);
    Check(!CrouchRules.Support(world,float.NaN,50,1.25f).Ground);
});
Run("Crouch edge guard stops at gaps in both directions without moving supported feet", () => {
    var world=new TestWorld(); world[30,50]=new(BlockShape.Solid);
    Check(CrouchRules.GuardStep(world,30,50,1.25f,.5f)==.5f);
    float right=CrouchRules.GuardStep(world,30.9f,50,1.25f,.5f);
    Check(right<.11f && right>=0 && CrouchRules.Support(world,30.9f+right,50,1.25f).Ground);
    float left=CrouchRules.GuardStep(world,28.85f,50,1.25f,-.5f);
    Check(left>-.11f && left<=0 && CrouchRules.Support(world,28.85f+left,50,1.25f).Ground);
});
Run("Crouch pose keeps feet and square head stable while mirroring the torso lean", () => {
    Check(CrouchRules.Offset(12,1,1)==Vector2.Zero);
    Check(CrouchRules.Offset(42,1,1)==new Vector2(6,4));
    Check(CrouchRules.Offset(35,1,1)==CrouchRules.Offset(42,1,1));
    Check(CrouchRules.Offset(24.5f,1,-1)==new Vector2(-3,2));
    Check(CrouchRules.Offset(42,0,1)==Vector2.Zero);
    Check(CrouchRules.Offset(float.NaN,1,1)==Vector2.Zero);
});
Run("Villager egg keeps the native egg silhouette and chest UV art has safe multi-tile padding", () => {
    byte[] egg=ContentPixelArt.VillagerEgg(),sheep=ContentPixelArt.SheepEgg();
    Check(egg.Length==16*24*4 && !egg.SequenceEqual(sheep));
    for(int p=3;p<egg.Length;p+=4) Check(egg[p]==sheep[p]);
    byte[] uv=new byte[64*64*4];
    for(int y=0;y<64;y++) for(int x=0;x<64;x++) { int p=(x+y*64)*4;uv[p]=(byte)x;uv[p+1]=(byte)y;uv[p+3]=255; }
    byte[] front=ChestArt.Face(uv,1),top=ChestArt.Face(uv,0),side=ChestArt.Face(uv,2),atlas=ChestArt.Atlas(uv);
    Check(atlas.Length==36*114*4 && !front.SequenceEqual(top) && !front.SequenceEqual(side));
    Check(front[(7+4*16)*4]==1 && front[(7+4*16)*4+1]==1);
    for(int y=0;y<114;y++) for(int x=0;x<36;x++) if(x%18>=16 || y%38 is 16 or 17 or 36 or 37) Check(atlas[(x+y*36)*4+3]==0);
    for(int state=0;state<3;state++) for(int row=0;row<2;row++) for(int col=0;col<2;col++)
        Check(Enumerable.Range(0,256).Any(i=>atlas[((col*18+i%16)+(state*38+row*18+i/16)*36)*4+3]==255));
    Check(ContentPixelArt.TexturedCube(top,front,side).Length==24*24*4);
});
Run("Sprite matte cleanup removes transparent RGB without whitening opaque details", () => {
    byte[] raw=new byte[16*16*4];
    for(int p=0;p<raw.Length;p+=4){raw[p]=raw[p+1]=raw[p+2]=255;raw[p+3]=2;}
    for(int y=3;y<13;y++)for(int x=3;x<13;x++){int p=(x+y*16)*4;raw[p]=42;raw[p+1]=90;raw[p+2]=25;raw[p+3]=255;}
    int eye=(8+6*16)*4;raw[eye]=raw[eye+1]=raw[eye+2]=255;
    byte[] clean=SpriteEdges.Clean(raw,16,16);
    Check(clean.Take(4).All(b=>b==0) && clean[(3+3*16)*4+3]==0 && clean[eye]==255 && clean[eye+3]==255);
    Check(clean[(7+8*16)*4]==42 && raw[3]==2);
});
Run("Placement aim selects empty sheet cells and the nearest in-plane edge", () => {
    var map=new TestInteractionWorld();
    Check(PlacementRules.Aim(map,new(30.5f,40.5f,4),-Vector3.UnitZ,false,out var cell) && cell==new GridCell(30,40));
    map[30,40]=new(true,false,BlockShape.Solid);
    Check(PlacementRules.Aim(map,new(30.5f,40.1f,4),-Vector3.UnitZ,false,out cell) && cell==new GridCell(30,39));
    Check(PlacementRules.Aim(map,new(30.9f,40.5f,4),-Vector3.UnitZ,false,out cell) && cell==new GridCell(31,40));
    Check(PlacementRules.Aim(map,new(30.5f,40.5f,4),-Vector3.UnitZ,true,out cell) && cell==new GridCell(30,40));
    Check(!PlacementRules.Aim(map,new(float.NaN,40,4),-Vector3.UnitZ,false,out _));
    Check(!PlacementRules.Aim(map,new(30,40,8),-Vector3.UnitZ,false,out _));
    Check(!PlacementRules.Aim(map,new(30,40,4),Vector3.UnitX,false,out _));
});
Run("Placement reach checks the entire furniture footprint and fog", () => {
    Check(PlacementRules.InReach(new(30.5f,40.5f,5),30,40,1,1,false));
    Check(!PlacementRules.InReach(new(30.5f,40.5f,5),30,40,5,1,false));
    Check(!PlacementRules.InReach(new(float.NaN,40,5),30,40,1,1,false));
    var fog=new ExplorationMask(100,100,(x,y)=>x==30 && y==40);
    Check(PlacementRules.InReach(new(30.5f,40.5f,2),30,40,1,1,false,fog));
    Check(!PlacementRules.InReach(new(30.5f,40.5f,2),30,40,2,1,false,fog));
    var hidden=new TestInteractionWorld { RejectHiddenReads=true };
    Check(!PlacementRules.Aim(hidden,new(30.5f,40.5f,2),-Vector3.UnitZ,false,out _,new ExplorationMask(100,100,(x,y)=>false)));
});
Run("Surface background uses native pixel scale rather than a fixed 140-tile height", () => {
    Check(SurfaceBackdrop.Height(200,2.5f)==31.25f);
    Check(SurfaceBackdrop.Height(400,2.5f)==62.5f);
    Check(SurfaceBackdrop.Height(200,float.NaN)==0);
    var camera=SurfaceBackdrop.Camera(220,250,1200,1080);
    Check(camera.Top==186.25f && Math.Abs(camera.Magic-(-2680d/4000))<.00001);
    Check(Math.Abs(SurfaceBackdrop.WorldTop(camera,320)-206.25f)<.001f);
    Check(SurfaceBackdrop.Camera(float.NaN,250,1200,1080)==default);
});
Run("Surface background follows projected elevation and physical viewport size", () => {
    var low=SurfaceBackdrop.Camera(220,250,1200,1080);var high=SurfaceBackdrop.Camera(120,250,1200,1080);
    Check(low.Top-high.Top==100 && high.Magic>low.Magic);
    var tall=SurfaceBackdrop.Camera(220,250,1200,1440);
    Check(tall.Top<low.Top && float.IsFinite(tall.Adjustment));
});
Run("Firework cannot interrupt departure or consume an initial boost", () => {
    var flight=new FlightState(new(50),new(1.25f,2.625f));
    Check(!flight.UseFirework() && flight.BoostVelocity==Vector3.Zero);
});
Run("Firework propels toward the full camera direction and decays without input", () => {
    var world=new TestWorld(); var flight=new FlightState(new(50),new(1.25f,2.625f));
    for(int i=0;i<24;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    flight.Look(.7f,.4f);Vector3 origin=flight.Position,forward=flight.Forward;
    Check(flight.UseFirework());flight.Update(world,1f/60,Vector3.Zero,false);
    Check(Vector3.Dot(Vector3.Normalize(flight.Position-origin),forward)>.999f);
    Check(flight.BoostVelocity.Length()>30 && flight.BoostVelocity.Length()<32);
    for(int i=0;i<300;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    Check(flight.BoostVelocity==Vector3.Zero);
});
Run("Repeated fireworks have a finite speed cap and can redirect momentum", () => {
    var world=new TestWorld();var flight=new FlightState(new(50),new(1.25f,2.625f));
    for(int i=0;i<24;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    for(int i=0;i<100;i++)Check(flight.UseFirework());
    Check(Math.Abs(flight.BoostVelocity.Length()-60)<.001f);
    flight.Look(MathF.PI/2,0);flight.UseFirework();
    Check(flight.BoostVelocity.X>20 && flight.BoostVelocity.Length()<=60.001f);
});
Run("Firework boost cannot tunnel through the sheet and clears blocked momentum", () => {
    var world=new TestWorld();for(int x=10;x<90;x++)for(int y=10;y<90;y++)world[x,y]=new(BlockShape.Solid,HasWall:true);
    var flight=new FlightState(new(50),new(1.25f,2.625f),16);
    for(int i=0;i<24;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    flight.UseFirework();flight.UseFirework();
    for(int i=0;i<120;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    Check(flight.Position.Z>=SpatialRules.MinFlightDepth-.001f && SpatialRules.CanOccupyFlight(world,flight.Position,flight.BodySize));
    Check(flight.BoostVelocity==Vector3.Zero);
});
Run("Firework observes world air walls and ordinary controls keep working afterward", () => {
    var world=new TestWorld();var flight=new FlightState(new(88,50),new(1.25f,2.625f));
    for(int i=0;i<24;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    flight.Look(MathF.PI/2,0);flight.UseFirework();
    for(int i=0;i<120;i++)flight.Update(world,1f/60,Vector3.Zero,false);
    Check(flight.Position.X<89.376f && flight.BoostVelocity==Vector3.Zero);
    float before=flight.Position.X;flight.Update(world,1f/60,new(0,0,-1),false);
    Check(flight.Position.X<before);
});
Console.WriteLine($"PASS: {passed} spatial, flight, lifecycle, targeting, pose, interaction, combat, environment and content checks.");

bool Perceives(float distance, bool detached = false, bool tracking = false, bool visible = true) =>
    EnemyPerception.CanTarget(Vector2.Zero, new(distance, 0), new(true, false, false), detached, tracking, visible, 640, 1024);

ReturnFailure Validate(TestWorld world) => SpatialRules.ValidateReturn(world, new(30.5f, 30.5f), new(1.25f, 2.625f));
void Check(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception exception) { Console.Error.WriteLine("FAIL " + name + ": " + exception.Message); Environment.Exit(1); }
}

sealed class BoatWorld : IBoatWorld
{
    private readonly Dictionary<(int, int), BoatCell> cells = new();
    public int Width => 100;
    public int Height => 100;
    public bool RejectReads;
    public BoatCell this[int x, int y] { set => cells[(x, y)] = value; }
    public void Floor(BoatCell cell) { for (int x = 29; x <= 31; x++) cells[(x, 50)] = cell; }
    public BoatCell BoatCellAt(int x, int y) {
        if (RejectReads || x < 0 || y < 0 || x >= Width || y >= Height) throw new InvalidOperationException("Unsafe boat terrain read");
        return cells.GetValueOrDefault((x, y));
    }
}

sealed class TestWorld : ICollisionWorld
{
    private readonly Dictionary<(int, int), WorldCell> cells = new();
    public int Width => 100;
    public int Height => 100;
    public WorldCell this[int x, int y] { set => cells[(x, y)] = value; }
    public WorldCell GetCell(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) throw new IndexOutOfRangeException();
        return cells.GetValueOrDefault((x, y));
    }
}

sealed class TestBody : IPlayerBody
{
    public PlayerBodyState State { get; set; } = new(new(30, 30), new(2, 4), false, false, false, 12, 8);
}

sealed class TestTargetPlayer : ITargetablePlayer
{
    public TargetPresence Presence { get; set; } = new(true, false, false);
}

sealed class TestAggro : IAggroMemory
{
    public int Target { get; set; }
    public int PreviousTarget { get; set; }
    public bool HasAimArea { get; set; }
    public void ClearTargetArea() => HasAimArea = false;
}

sealed class TestInteractionWorld : IInteractionWorld
{
    private readonly Dictionary<(int, int), InteractionCell> cells = new();
    public int Width => 100;
    public int Height => 100;
    public bool RejectHiddenReads;
    public InteractionCell this[int x, int y] { set => cells[(x, y)] = value; }
    public InteractionCell GetInteractionCell(int x, int y)
    {
        if (RejectHiddenReads) throw new InvalidOperationException("Hidden terrain must not be inspected");
        if (x < 0 || y < 0 || x >= Width || y >= Height) throw new IndexOutOfRangeException();
        return cells.GetValueOrDefault((x, y));
    }
}

sealed class EnvironmentWorld : IEnvironmentWorld
{
    public int Width => 4000;
    public int Height => 2000;
    public bool RejectReads;
    public ProjectedBiome MaterialAt(int x, int y)
    {
        if (RejectReads) throw new InvalidOperationException("Invalid background projection read world tiles");
        return x >= 1500 ? ProjectedBiome.Snow : ProjectedBiome.Unknown;
    }
}

sealed class HiddenSpawnWorld : ICollisionWorld
{
    public int Width => 100;
    public int Height => 100;
    public WorldCell GetCell(int x, int y) => throw new InvalidOperationException("Hidden spawn terrain was read");
}

sealed class SummonWorld : IGolemStructureWorld
{
    public int Width => 100;
    public int Height => 100;
    public Dictionary<StructureTile, GolemStructureCell> Structures = new() {
        [new(30, 50)] = GolemStructureCell.Pumpkin, [new(29, 51)] = GolemStructureCell.IronBlock,
        [new(30, 51)] = GolemStructureCell.IronBlock, [new(31, 51)] = GolemStructureCell.IronBlock,
        [new(30, 52)] = GolemStructureCell.IronBlock };
    public Dictionary<StructureTile, WorldCell> Terrain = new() { [new(30, 53)] = new(BlockShape.Solid) };
    public List<StructureTile> Consumed = new();
    public StructureTile? Protected;
    public bool FullNpcs, RejectReads;
    public int Spawns;
    public Vector2 SpawnCenter;
    public GolemStructureCell StructureAt(int x, int y)
    {
        if (RejectReads) throw new InvalidOperationException("Invalid structure coordinates read tiles");
        return Structures.GetValueOrDefault(new(x, y));
    }
    public WorldCell GetCell(int x, int y) => Terrain.TryGetValue(new(x, y), out var cell) ? cell :
        new(Structures.ContainsKey(new(x, y)) ? BlockShape.Solid : BlockShape.Empty);
    public bool CanConsume(int x, int y) => Protected != new StructureTile(x, y);
    public bool TrySpawn(Vector2 center)
    {
        if (FullNpcs) return false;
        // The live adapter reserves the NPC before it clears the foreground cells.
        if (Structures.Count != 5) throw new InvalidOperationException("Materials were spent before reserving the NPC");
        Spawns++; SpawnCenter = center; return true;
    }
    public void Consume(IReadOnlyList<StructureTile> cells)
    {
        if (Spawns != 1) throw new InvalidOperationException("Removal without a reserved NPC");
        foreach (var cell in cells)
        {
            WorldCell old = GetCell(cell.X, cell.Y);
            Terrain[cell] = old with { Shape = BlockShape.Empty };
            Structures.Remove(cell); Consumed.Add(cell);
        }
    }
}
