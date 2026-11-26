using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._TP.Kitchen.Components;

[RegisterComponent]
public sealed partial class CoffeeMakerComponent : Component
{
    [DataField]
    public SoundPathSpecifier? FinishSound = new("/Audio/_TP/Machines/Kitchen/coffee-pour.ogg");

    [DataField]
    public SoundPathSpecifier? BrewSound = new("/Audio/_Floof/Machines/Kitchen/coffee-brew.ogg");

    [DataField]
    public TimeSpan BrewTime = TimeSpan.FromSeconds(8);

    [DataField]
    public TimeSpan? BrewEnd;

    public EntityUid? PlayingStream;
}

[Serializable, NetSerializable]
public enum CoffeeMakerVisuals : byte
{
    Basket,
    Beaker,
    Pitcher,
    Brewing,
}
