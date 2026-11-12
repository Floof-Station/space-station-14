using System.Linq;
using Content.Server._Floof.ChildEntities;
using Content.Server._Floof.Link;
using Content.Shared._Floof.Link;
using Content.Shared.Atmos.Components;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Floof.Expo;

public sealed partial class ExpoLinkSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;

    [SubscribeLocalEvent]
    private void OnLinkChanged(Entity<ExpoLinkComponent> ent, ref LinkedEntityChangedEvent args)
    {
        PairChildren(ent);
    }

    [SubscribeLocalEvent]
    private void OnChildrenSpawned(Entity<ExpoLinkComponent> ent, ref AnchoredChildrenSpawnedEvent args)
    {
        PairChildren(ent);
    }

    /// <summary>
    /// Pairs each child with the partner's child at the same index.
    /// </summary>
    private void PairChildren(EntityUid uid)
    {
        if (!_linked.GetLink(uid, out var partner)
            || !TryComp<AnchoredChildEntitiesComponent>(uid, out var ours)
            || !TryComp<AnchoredChildEntitiesComponent>(partner, out var theirs))
            return;

        if (HasComp<ExpoLinkCoreComponent>(uid) == HasComp<ExpoLinkCoreComponent>(partner))
        {
            Log.Error($"{ToPrettyString(uid)} is linked to {ToPrettyString(partner)}, but an Expo link needs one anchor and one core.");
            return;
        }

        // the side that hasn't spawned its children yet pairs once it does
        if (ours.Children.Count == 0 || theirs.Children.Count == 0)
            return;

        if (ours.Children.Count != theirs.Children.Count)
        {
            Log.Error($"{ToPrettyString(uid)} has {ours.Children.Count} children but its partner {ToPrettyString(partner)} has {theirs.Children.Count}, children not paired.");
            return;
        }

        for (var i = 0; i < ours.Children.Count; i++)
        {
            var child = ours.Children[i];
            var other = theirs.Children[i];

            if (_linked.GetLink(child, out var current) && current == other)
                continue;

            if (!Matches(child, other))
            {
                Log.Error($"Expo link child {i} doesn't match: {ToPrettyString(child)} and {ToPrettyString(other)}, not paired.");
                continue;
            }

            Unlink(child);
            Unlink(other);
            _linked.TryLink(child, other);
        }
    }

    /// <summary>
    /// Unlinks all children of this end, which also unlinks the partner's.
    /// </summary>
    public void UnpairChildren(EntityUid uid)
    {
        if (!TryComp<AnchoredChildEntitiesComponent>(uid, out var children))
            return;

        foreach (var child in children.Children)
            Unlink(child);
    }

    private bool Matches(EntityUid a, EntityUid b)
    {
        if (HasComp<StaticPortalComponent>(a) || HasComp<StaticPortalComponent>(b))
            return HasComp<StaticPortalComponent>(a) && HasComp<StaticPortalComponent>(b);

        if (HasComp<PowerLinkComponent>(a) || HasComp<PowerLinkComponent>(b))
        {
            return TryComp<PowerLinkComponent>(a, out var powerA)
                   && TryComp<PowerLinkComponent>(b, out var powerB)
                   && powerA.Direction != powerB.Direction;
        }

        if (HasComp<GasLinkComponent>(a) || HasComp<GasLinkComponent>(b))
        {
            return HasComp<GasLinkComponent>(a)
                   && HasComp<GasLinkComponent>(b)
                   && TryComp<AtmosPipeLayersComponent>(a, out var layerA)
                   && TryComp<AtmosPipeLayersComponent>(b, out var layerB)
                   && layerA.CurrentPipeLayer == layerB.CurrentPipeLayer;
        }

        return false;
    }

    private void Unlink(EntityUid uid)
    {
        if (!TryComp<LinkedEntityComponent>(uid, out var link))
            return;

        foreach (var other in link.LinkedEntities.ToArray())
            _linked.TryUnlink(uid, other, link);
    }
}
