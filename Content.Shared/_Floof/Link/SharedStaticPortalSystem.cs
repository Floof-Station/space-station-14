using System.Linq;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Components;
using Content.Shared.Teleportation.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Misc;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.Link;

/// <summary>
/// This handles teleporting entities from a portal to a linked portal.
/// Uses <see cref="LinkedEntitySystem"/> to get linked portals.
/// </summary>
/// <seealso cref="StaticPortalComponent"/>
public abstract partial class SharedStaticPortalSystem : EntitySystem
{
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedJointSystem _joints = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TagSystem _tag = default!;

    private const string PortalFixture = "portalFixture";
    private const string ProjectileFixture = "projectile";
    private static readonly ProtoId<TagPrototype> ShowTraverseVerbTag = "AllowPortalTraversal";
    private static readonly ProtoId<TagPrototype> PreventCollisionTag = "PreventPortalCollision";
    
    [SubscribeLocalEvent]
    private void OnGetVerbs(Entity<StaticPortalComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        // Traversal altverb for ghosts to use that bypasses normal functionality
        if (!args.CanAccess || !_tag.HasTag(args.User, ShowTraverseVerbTag))
            return;

        // Don't use the verb with unlinked or with multi-output portals
        // (this is only intended to be useful for ghosts to see where a linked portal leads)
        var disabled = !TryComp<LinkedEntityComponent>(ent, out var link) || link.LinkedEntities.Count != 1;

        var subject = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Priority = 11,
            Act = () =>
            {
                if (link == null || disabled)
                    return;

                // check prediction
                if (_netMan.IsClient && !CanPredictTeleport((ent, link)))
                    return;

                var destination = link.LinkedEntities.First();

                TeleportEntity(ent, subject, Transform(destination).Coordinates, destination, false);
            },
            Disabled = disabled,
            Text = Loc.GetString("portal-component-ghost-traverse"),
            Message = disabled
                ? Loc.GetString("portal-component-no-linked-entities")
                : Loc.GetString("portal-component-can-ghost-traverse"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/open.svg.192dpi.png"))
        });
    }
    
    [SubscribeLocalEvent]
    private void OnCollide(Entity<StaticPortalComponent> ent, ref StartCollideEvent args)
    {
        if (!ShouldCollide(args.OurFixtureId, args.OtherFixtureId, args.OurFixture, args.OtherFixture))
            return;

        var subject = args.OtherEntity;

        if (_tag.HasTag(args.OtherEntity, PreventCollisionTag))
            return;

        // best not.
        if (Transform(subject).Anchored)
            return;

        // break pulls before portal enter so we don't break shit
        if (TryComp<PullableComponent>(subject, out var pullable) && pullable.BeingPulled)
        {
            _pulling.TryStopPull(subject, pullable);
        }

        if (TryComp<PullerComponent>(subject, out var pullerComp)
            && TryComp<PullableComponent>(pullerComp.Pulling, out var subjectPulling))
        {
            _pulling.TryStopPull(pullerComp.Pulling.Value, subjectPulling);
        }

        // also break grapple joints
        _joints.RemoveJoint(subject, SharedGrapplingGunSystem.GrapplingJoint);

        // if they came from another portal, just return and wait for them to exit the portal
        if (HasComp<StaticPortalTimeoutComponent>(subject))
        {
            return;
        }

        if (TryComp<LinkedEntityComponent>(ent, out var link) && link.LinkedEntities.Count != 0)
        {
            // check prediction
            if (_netMan.IsClient && !CanPredictTeleport((ent, link)))
                return;

            // pick a target and teleport there
            var target = _random.Pick(link.LinkedEntities);

            if (HasComp<StaticPortalComponent>(target))
            {
                // if target is a portal, signal that they shouldn't be immediately teleported back
                var timeout = EnsureComp<StaticPortalTimeoutComponent>(subject);
                timeout.EnteredPortal = ent;
                Dirty(subject, timeout);
            }

            TeleportEntity(ent, subject, Transform(target).Coordinates, target);
        }
    }
    
    [SubscribeLocalEvent]
    private void OnEndCollide(Entity<StaticPortalComponent> ent, ref EndCollideEvent args)
    {
        if (!ShouldCollide(args.OurFixtureId, args.OtherFixtureId, args.OurFixture, args.OtherFixture))
            return;

        var subject = args.OtherEntity;

        // if they came from a different portal, remove the timeout
        if (TryComp<StaticPortalTimeoutComponent>(subject, out var timeout) && timeout.EnteredPortal != ent)
        {
            RemCompDeferred<StaticPortalTimeoutComponent>(subject);
        }
    }

    /// <summary>
    /// Checks if the colliding fixtures are the ones we want.
    /// </summary>
    /// <returns>
    /// False if our fixture is not a portal fixture.
    /// False if other fixture is not hard, but makes an exception for projectiles.
    /// </returns>
    private bool ShouldCollide(string ourId, string otherId, Fixture our, Fixture other)
    {
        return ourId == PortalFixture && (other.Hard || otherId == ProjectileFixture);
    }

    /// <summary>
    /// Checks if the client is able to predict the teleport.
    /// Client can't predict outside 1-to-1 portal-to-portal interactions due to randomness involved.
    /// </summary>
    /// <returns>
    /// False if the linked entity count isn't 1.
    /// False if the linked entity doesn't exist on the client / is outside PVS.
    /// </returns>
    private bool CanPredictTeleport(Entity<LinkedEntityComponent> portal)
    {
        var first = portal.Comp.LinkedEntities.First();
        var exists = Exists(first);

        if (!exists ||
            portal.Comp.LinkedEntities.Count != 1 || // 0 and >1 use RNG
            exists && Transform(first).MapID == MapId.Nullspace) // The linked entity is most likely outside PVS
            return false;

        return true;
    }

    /// <summary>
    /// Handles teleporting a subject from the portal entity to a coordinate.
    /// </summary>
    /// <param name="ent"> The portal being collided with. </param>
    /// <param name="subject"> The entity getting teleported. </param>
    /// <param name="target"> The location to teleport to. </param>
    /// <param name="targetEntity"> The portal on the other side of the teleport. </param>
    private void TeleportEntity(Entity<StaticPortalComponent> ent, EntityUid subject, EntityCoordinates target, EntityUid? targetEntity = null, bool playSound = true)
    {
        var arrivalSound = CompOrNull<StaticPortalComponent>(targetEntity)?.ArrivalSound ?? ent.Comp.ArrivalSound;
        var departureSound = ent.Comp.DepartureSound;

        LogTeleport(ent, subject, Transform(subject).Coordinates, target);

        _transform.SetCoordinates(subject, target);
        RaiseLocalEvent(subject, new TeleportedEvent { Subject = subject }, true); // Floofstation. Note that this is raised broadcast.

        if (!playSound)
            return;

        _audio.PlayPredicted(departureSound, ent, subject);
        _audio.PlayPredicted(arrivalSound, subject, subject);
    }
    
    protected virtual void LogTeleport(EntityUid portal, EntityUid subject, EntityCoordinates source, EntityCoordinates target)
    {
    }
}
