using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._TP.Kitchen.Prototypes;

[Prototype]
public sealed partial class CoffeeMakerRecipePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CoffeeMakerRecipePrototype>))]
    public string[]? Parents { get; private set; }

    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Input;

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Outputs = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Byproducts = new();

    [DataField]
    public Dictionary<CoffeeQuality, Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>>? Quality;
}

[Serializable, NetSerializable]
public enum CoffeeQuality : byte
{
    Low,
    Medium,
    High,
}
