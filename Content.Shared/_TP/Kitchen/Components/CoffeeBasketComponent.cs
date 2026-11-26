using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._TP.Kitchen.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CoffeeBasketComponent : Component
{
    [DataField, AutoNetworkedField]
    public CoffeePrep Prep = CoffeePrep.None;

    [DataField]
    public TimeSpan PrepTime = TimeSpan.FromSeconds(0.5);
}

[Flags, Serializable, NetSerializable]
public enum CoffeePrep : byte
{
    None = 0,
    Stirred = 1 << 0,
    Tamped = 1 << 1,
}

[Serializable, NetSerializable]
public sealed partial class CoffeePrepDoAfterEvent : SimpleDoAfterEvent;
