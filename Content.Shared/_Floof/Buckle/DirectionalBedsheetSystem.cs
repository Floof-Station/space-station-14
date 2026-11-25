using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Placeable;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;

namespace Content.Shared._Floof.Buckle;

public sealed partial class DirectionalBedsheetSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [SubscribeLocalEvent(after: [typeof(PlaceableSurfaceSystem)])]
    private void OnAfterInteractUsing(Entity<DirectionalBedComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!args.Handled
            || !HasComp<DirectionalBedsheetComponent>(args.Used)
            || _container.IsEntityInContainer(args.Used))
            return;

        _transform.SetWorldRotation(args.Used, _transform.GetWorldRotation(ent));
    }

    [SubscribeLocalEvent]
    private void OnDropped(Entity<DirectionalBedsheetComponent> ent, ref DroppedEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<DirectionalBedComponent>(uid))
                continue;

            _transform.SetWorldRotation(ent, _transform.GetWorldRotation(uid.Value));
            return;
        }
    }
}
