using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Link;

/// <summary>
///     Marks an entity as being a 'static portal' which teleports entities sent through it to linked entities.
///     Just like a normal portal but with benefits.
///     Relies on <see cref="LinkedEntityComponent"/> being set up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StaticPortalComponent : Component
{
    /// <summary>
    ///     Sound played on arriving to this portal, centered on the destination.
    ///     The arrival sound of the entered portal will play if the destination is not a portal.
    /// </summary>
    [DataField]
    public SoundSpecifier ArrivalSound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");

    /// <summary>
    ///     Sound played on departing from this portal, centered on the original portal.
    /// </summary>
    [DataField]
    public SoundSpecifier DepartureSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}
