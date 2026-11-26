using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared._TP.Kitchen.Prototypes;

[Prototype]
public sealed partial class CoffeeMakerRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Input;

    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Output;

    [DataField]
    public ProtoId<ReagentPrototype>? Byproduct;
}
