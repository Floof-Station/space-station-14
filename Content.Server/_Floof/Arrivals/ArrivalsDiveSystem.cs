using System.Numerics;
using Content.Server._Floof.MapLayer;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Station.Systems;
using Content.Shared._Floof.DayNightCycle;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.Ocean;
using Content.Shared.Camera;
using Content.Shared.Light.Components;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Takes a diving arrivals shuttle down to the station and back up to the terminal. It sails through a map of its own,
/// really moving, then docks at the other end.
/// </summary>
public sealed partial class ArrivalsDiveSystem : EntitySystem
{
    [Dependency] private DayNightCycleSystem _dayNight = default!;
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private OceanSeaSystem _ocean = default!;
    [Dependency] private ServerArrivalsSystem _arrivals = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private StationSystem _station = default!;

    private const int LandingShakes = 10;
    private const float LandingShakeStrength = 0.6f;
    private static readonly TimeSpan LandingShakeInterval = TimeSpan.FromSeconds(0.15);

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(MapLayerSystem));

        SubscribeLocalEvent<ArrivalsDiveComponent, ArrivalsTripEvent>(OnTrip);
        SubscribeLocalEvent<ArrivalsDiveComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<ArrivalsDiveComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TransitionLayer is { } layer)
            _layers.FreeTransitionLayer(layer);
    }

    private void OnTrip(Entity<ArrivalsDiveComponent> ent, ref ArrivalsTripEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.TravelTime = ent.Comp.DiveTime;

        var layer = TakeTransitionLayer(ent);
        var depth = EnsureComp<OceanDepthComponent>(layer);
        var now = _timing.CurTime;
        // As deep as the station's sea floor, if it has one.
        var stationMap = GetStationMap(args.Station);
        var floor = CompOrNull<OceanDepthComponent>(stationMap);
        if (floor != null)
            depth.FloorDepth = floor.FloorDepth;
        depth.StartDepth = args.ToStation ? 0f : depth.FloorDepth;
        depth.Rate = (args.ToStation ? 1f : -1f) * depth.FloorDepth / (float) ent.Comp.DiveTime.TotalSeconds;
        depth.StartTime = now;
        depth.SurfaceLight = GetSurfaceLight(args.Terminal);
        Dirty(layer, depth);

        ent.Comp.Target = args.Target;
        ent.Comp.ToStation = args.ToStation;
        ent.Comp.ArriveTime = now + ent.Comp.DiveTime;

        _arrivals.TripDeparted(ent.Owner, args.Target, ent.Comp.DiveTime);

        _dock.UndockDocks(ent);
        var xform = Transform(ent);
        var (position, rotation) = _xform.GetWorldPositionRotation(xform);

        _layers.RemoveLayer(layer);
        if (floor is { Seabed: true })
            DiveOverStation(ent, layer, stationMap!.Value, floor, args, ref position, ref rotation);

        _xform.SetCoordinates(ent, xform, new EntityCoordinates(layer, position), rotation);
        _shuttles.Enable(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ArrivalsDiveComponent, ShuttleComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var dive, out var shuttle, out var body))
        {
            if (dive.ShakesLeft > 0 && now >= dive.NextShake)
                Shake((uid, dive));

            if (dive.Target is not { } target)
                continue;

            if (now < dive.ArriveTime)
            {
                Sail((uid, dive, body), now);
                continue;
            }

            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
            _physics.SetAngularVelocity(uid, 0f, body: body);

            dive.Target = null;
            Arrive((uid, dive), shuttle, target);
        }
    }

    /// <summary>
    /// Holds the speed the trip calls for right now, which also keeps tile friction from slowing it.
    /// </summary>
    private void Sail(Entity<ArrivalsDiveComponent, PhysicsComponent> ent, TimeSpan now)
    {
        var dive = ent.Comp1;
        var progress = 1f - (float) ((dive.ArriveTime - now).TotalSeconds / dive.DiveTime.TotalSeconds);
        var share = MathF.Min(1f, MathF.Min(progress / dive.SpeedUpShare, (1f - progress) / dive.SlowDownShare));
        var forward = _xform.GetWorldRotation(ent).RotateVec(Vector2.UnitY);
        _physics.SetLinearVelocity(ent, forward * dive.Speed * Math.Clamp(share, 0f, 1f), body: ent.Comp2);
        _physics.SetAngularVelocity(ent, 0f, body: ent.Comp2);
    }

    private void Arrive(Entity<ArrivalsDiveComponent> ent, ShuttleComponent shuttle, EntityUid target)
    {
        if (Exists(target))
            _shuttles.TryFTLDock(ent, shuttle, target, ent.Comp.ToStation ? "DockArrivals" : null);

        if (ent.Comp.TransitionLayer is { } layer)
        {
            if (Transform(ent).MapUid == layer)
            {
                _layers.RemoveLayer(layer);
            }
            else
            {
                _layers.FreeTransitionLayer(layer);
                ent.Comp.TransitionLayer = null;
            }
        }

        if (ent.Comp.ToStation)
        {
            ent.Comp.ShakesLeft = LandingShakes;
            ent.Comp.NextShake = _timing.CurTime;
        }

        _arrivals.TripArrived(ent.Owner);
    }

    /// <summary>
    /// One hard jolt that dies down, for everyone aboard.
    /// </summary>
    private void Shake(Entity<ArrivalsDiveComponent> ent)
    {
        var strength = LandingShakeStrength * ent.Comp.ShakesLeft / LandingShakes;
        ent.Comp.ShakesLeft--;
        ent.Comp.NextShake = _timing.CurTime + LandingShakeInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } player && Transform(player).GridUid == ent.Owner)
                _recoil.KickCamera(player, _random.NextAngle().ToVec() * strength);
        }
    }

    private EntityUid TakeTransitionLayer(Entity<ArrivalsDiveComponent> ent)
    {
        if (ent.Comp.TransitionLayer is { } held && Exists(held))
            return held;

        var map = _layers.TakeTransitionLayer(ent);
        _ocean.CopyOcean(map);

        ent.Comp.TransitionLayer = map;
        return map;
    }

    /// <summary>
    /// </summary>
    private void DiveOverStation(Entity<ArrivalsDiveComponent> ent,
        EntityUid layer,
        EntityUid stationMap,
        OceanDepthComponent floor,
        ArrivalsTripEvent args,
        ref Vector2 position,
        ref Angle rotation)
    {
        var stack = (int) Transform(stationMap).MapID;
        _layers.SetLayer(stationMap, stack, 0f);
        if (!args.ToStation)
        {
            _layers.StartTransition(layer, stack, 0f, floor.FloorDepth, ent.Comp.DiveTime);
            return;
        }

        _layers.StartTransition(layer, stack, floor.FloorDepth, 0f, ent.Comp.DiveTime);
        if (_dock.GetDockingConfig(ent, args.Target, "DockArrivals") is not { } config)
            return;

        var dive = ent.Comp;
        var distance = dive.Speed * (float) dive.DiveTime.TotalSeconds * (1f - (dive.SpeedUpShare + dive.SlowDownShare) / 2f);

        rotation = config.Angle + _xform.GetWorldRotation(config.Coordinates.EntityId);
        position = _xform.ToMapCoordinates(config.Coordinates).Position - rotation.RotateVec(Vector2.UnitY) * distance;
    }

    private EntityUid? GetStationMap(EntityUid station)
    {
        return _station.GetLargestGrid(station) is { } grid ? Transform(grid).MapUid : null;
    }

    /// <summary>
    /// The daylight at the terminal, so the water at the top looks like the water the shuttle just left.
    /// </summary>
    private Color GetSurfaceLight(EntityUid terminal)
    {
        if (Transform(terminal).MapUid is not { } map)
            return Color.Black;

        if (TryComp<DayNightCycleComponent>(map, out var cycle))
            return _dayNight.GetColor((map, cycle));

        return CompOrNull<MapLightComponent>(map)?.AmbientLightColor ?? Color.Black;
    }
}
