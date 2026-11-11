using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Teleportation.Systems;

namespace Content.Server._Floof.Link;

public sealed partial class PowerLinkSystem : EntitySystem
{
    [Dependency] private LinkedEntitySystem _linked = default!;
    [Dependency] private PowerNetSystem _powerNet = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Network load lists are only rebuilt in PowerNetSystem.Update; reading stats before that hits freed loads.
        UpdatesAfter.Add(typeof(PowerNetSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PowerLinkComponent, PowerConsumerComponent>();
        while (query.MoveNext(out var uid, out var link, out var consumer))
        {
            if (link.Direction == PowerLinkDirection.Output)
                continue;

            if (!_linked.GetLink(uid, out var partner)
                || !TryComp<PowerLinkComponent>(partner, out var partnerLink)
                || partnerLink.Direction == PowerLinkDirection.Input
                || !TryComp<PowerSupplierComponent>(partner, out var supplier))
            {
                consumer.DrawRate = 0f;
                continue;
            }

            if (!link.Enabled || !partnerLink.Enabled)
            {
                consumer.DrawRate = 0f;
                supplier.MaxSupply = 0f;
                continue;
            }
            
            supplier.MaxSupply = consumer.ReceivedPower;

            var demand = supplier.Net is { } net
                ? _powerNet.GetNetworkStatistics(net.NetworkNode).Consumption
                : 0f;

            consumer.DrawRate = demand + link.Headroom;
        }
    }

    public void SetEnabled(Entity<PowerLinkComponent?> ent, bool enabled)
    {
        if (Resolve(ent, ref ent.Comp, false))
            ent.Comp.Enabled = enabled;
    }

    [SubscribeLocalEvent]
    private void OnLinkChanged(Entity<PowerLinkComponent> ent, ref LinkedEntityChangedEvent args)
    {
        // NewLinks contains all links not just new ones. 
        if (args.NewLinks.Count > 0 || !TryComp<PowerSupplierComponent>(ent, out var supplier))
            return;

        supplier.MaxSupply = 0f;
    }
}
