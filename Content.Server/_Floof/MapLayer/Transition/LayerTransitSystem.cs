using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.MapLayer;
using Content.Shared._Floof.MapLayer.Transition;
using Content.Shared._Floof.Ocean;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._Floof.MapLayer.Transition;

public sealed partial class LayerTransitSystem : EntitySystem
{
    [Dependency] private DockingSystem _dock = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MapLayerSystem _layers = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private OceanSeaSystem _ocean = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private List<Entity<MapGridComponent>> _blocking = new();

    public const float CruiseHeight = 10f;

    public static readonly TimeSpan LiftTime = TimeSpan.FromSeconds(10);

    [SubscribeLocalEvent]
    private void OnLift(EntityUid uid, ShuttleConsoleComponent comp, SubLiftMessage args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } sub || xform.MapUid is not { } seabed)
            return;

        if (!TryComp<OceanDepthComponent>(seabed, out var depth) || !depth.Seabed)
            return;

        EnsureCruiseLayer(seabed);
        var stack = (int) Transform(seabed).MapID;
        StartTransition(sub, (seabed, depth), stack, 0f, CruiseHeight);
    }

    [SubscribeLocalEvent]
    private void OnLand(EntityUid uid, ShuttleConsoleComponent comp, SubLandMessage args)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } sub ||
            xform.MapUid is not { } map ||
            !TryGetSeabedBelow(map, out var stack, out var z, out var seabed))
            return;

        if (IsBlocked(seabed, GetBounds(sub, 0f)))
        {
            _popup.PopupEntity(Loc.GetString("sub-helm-land-blocked"), uid, args.Actor);
            return;
        }

        var layer = StartTransition(sub, seabed, stack, z, 0f);
        _layers.AddPhantoms(layer, seabed, GetBounds(sub,  _cfg.GetCVar(CCVars.SubPhantomReach)));
    }

    private Box2Rotated GetBounds(EntityUid sub, float margin)
    {
        var (position, rotation) = _xform.GetWorldPositionRotation(sub);
        var local = Comp<MapGridComponent>(sub).LocalAABB.Enlarged(margin);
        return new Box2Rotated(local.Translated(position), rotation, position);
    }
    
    private bool IsBlocked(EntityUid map, Box2Rotated bounds)
    {
        _blocking.Clear();
        _map.FindGridsIntersecting(map, bounds, ref _blocking, includeMap: false);
        return _blocking.Count > 0;
    }
    
    [SubscribeLocalEvent]
    private void OnArrived(Entity<ShuttleComponent> ent, ref MapLayerArrivedEvent args)
    {
        if (!TryComp<OceanDepthComponent>(args.Layer, out var depth) || !depth.Seabed)
        {
            _shuttle.Enable(ent);
            return;
        }

        TryComp<DockApproachComponent>(ent, out var approach);
        if (approach != null)
            SnapToDock((ent, approach));

        Smimsh(ent);

        if (approach != null)
            ConnectDocks((ent, approach));
    }
    
    private EntityUid StartTransition(EntityUid sub, Entity<OceanDepthComponent> seabed, int stack, float fromZ, float toZ)
    {
        var layer = _layers.TakeTransitionLayer(sub);
        _ocean.CopyOcean(layer, seabed);

        var duration = GetDuration(fromZ, toZ);
        
        if (TryComp<OceanDepthComponent>(layer, out var depth))
        {
            depth.StartDepth = seabed.Comp.FloorDepth - fromZ;
            depth.StartTime = _timing.CurTime;
            depth.Rate = (fromZ - toZ) / (float) duration.TotalSeconds;
            Dirty(layer, depth);
        }

        _layers.StartTransition(layer, stack, fromZ, toZ, duration);
        Log.Info($"Moving {ToPrettyString(sub)} from Z {fromZ:0.0} to {toZ:0.0} through {ToPrettyString(layer)}");
        
        _shuttle.Enable(sub);
        _dock.UndockDocks(sub);
        var subXform = Transform(sub);
        var (position, rotation) = _xform.GetWorldPositionRotation(subXform);
        _xform.SetCoordinates(sub, subXform, new EntityCoordinates(layer, position), rotation);
        return layer;
    }
    
    private static TimeSpan GetDuration(float fromZ, float toZ)
    {
        return LiftTime * (MathF.Max(MathF.Abs(toZ - fromZ), 0.1f) / CruiseHeight);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<SubCruiseLayerComponent> ent, ref ComponentShutdown args)
    {
        QueueDel(ent.Comp.CruiseLayer);
    }
    
    public EntityUid EnsureCruiseLayer(EntityUid seabed)
    {
        var comp = EnsureComp<SubCruiseLayerComponent>(seabed);
        if (comp.CruiseLayer is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap();
        _ocean.CopyOcean(map, seabed);
        if (TryComp<OceanDepthComponent>(map, out var depth))
        {
            depth.StartDepth = depth.FloorDepth - CruiseHeight;
            Dirty(map, depth);
        }
        
        var stack = (int) Transform(seabed).MapID;
        _layers.SetLayer(seabed, stack, 0f);
        _layers.SetLayer(map, stack, CruiseHeight);

        _metaData.SetEntityName(map, Loc.GetString("sub-lift-cruise-layer", ("seabed", Name(seabed))));

        comp.CruiseLayer = map;
        return map;
    }
}
