using System.Linq;
using Content.Server._Floof.ChildEntities;
using Content.Server._Floof.Link;
using Content.Shared._Floof.Link;
using Content.Shared.Atmos.Components;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using StaticPortalSystem = Content.Server._Floof.Link.StaticPortalSystem;

namespace Content.Server._Floof.FathomGate;

public sealed partial class FathomGateSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;
    [Dependency] private PowerLinkSystem _powerLink = default!;
    [Dependency] private GasLinkSystem _gasLink = default!;
    [Dependency] private StaticPortalSystem _portal = default!;

    [SubscribeLocalEvent]
    private void OnLinkChanged(Entity<FathomGateComponent> ent, ref LinkedEntityChangedEvent args)
    {
        PairChildren(ent);
    }

    [SubscribeLocalEvent]
    private void OnChildrenSpawned(Entity<FathomGateComponent> ent, ref AnchoredChildrenSpawnedEvent args)
    {
        PairChildren(ent);
        ApplyToChildren(ent);
    }

    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<FathomGateComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var open = ent.Comp.State == FathomGateState.Open;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(open ? "fathom-gate-verb-close" : "fathom-gate-verb-open"),
            Act = () => SetState(ent.AsNullable(), open ? FathomGateState.Closed : FathomGateState.Open),
        });
    }

    /// <summary>
    /// Sets the state on this gate and its partner.
    /// </summary>
    public void SetState(Entity<FathomGateComponent?> ent, FathomGateState state)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        Apply((ent, ent.Comp), state);

        if (_linked.GetLink(ent, out var partner) && TryComp<FathomGateComponent>(partner, out var partnerGate))
            Apply((partner.Value, partnerGate), state);
    }

    private void Apply(Entity<FathomGateComponent> ent, FathomGateState state)
    {
        var old = ent.Comp.State;
        if (old == state)
            return;

        ent.Comp.State = state;

        if (old == FathomGateState.Collapsed)
            PairChildren(ent);

        ApplyToChildren(ent);

        if (state == FathomGateState.Collapsed)
            UnpairChildren(ent);

        switch (state)
        {
            case FathomGateState.Open:
                var opened = new FathomGateOpenedEvent();
                RaiseLocalEvent(ent, ref opened);
                break;
            case FathomGateState.Collapsed:
                var collapsed = new FathomGateCollapsedEvent();
                RaiseLocalEvent(ent, ref collapsed);
                break;
        }
    }

    private void ApplyToChildren(Entity<FathomGateComponent> ent)
    {
        if (!TryComp<AnchoredChildEntitiesComponent>(ent, out var children))
            return;

        var open = ent.Comp.State == FathomGateState.Open;
        foreach (var child in children.Children)
        {
            _powerLink.SetEnabled(child, open);
            _gasLink.SetEnabled(child, open);
            _portal.SetOpen(child, open ? 1f : 0f);
        }
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
                Log.Error($"Fathom gate child {i} doesn't match: {ToPrettyString(child)} and {ToPrettyString(other)}, not paired.");
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
            return HasComp<PowerLinkComponent>(a) && HasComp<PowerLinkComponent>(b);

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
