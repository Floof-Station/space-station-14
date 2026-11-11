using Content.Shared._Floof.Link;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Floof.Link;

public sealed partial class LinkKeySystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;
    
    [SubscribeLocalEvent] //I love this tag!
    private void OnMapInit(Entity<LinkKeyComponent> ent, ref MapInitEvent args)
    {
        if (IsLinked(ent))
            return;

        var query = EntityQueryEnumerator<LinkKeyComponent>();
        while (query.MoveNext(out var uid, out var other))
        {
            if (uid == ent.Owner || other.Key != ent.Comp.Key || IsLinked(uid))
                continue;

            _linked.TryLink(ent, uid);
            return;
        }
    }

    private bool IsLinked(EntityUid uid)
    {
        return TryComp<LinkedEntityComponent>(uid, out var link) && link.LinkedEntities.Count > 0;
    }
}
