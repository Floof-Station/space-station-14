namespace Content.Server._Floof.MapLayer.Transition;

[RegisterComponent]
public sealed partial class SubCruiseLayerComponent : Component
{
    [DataField]
    public EntityUid? CruiseLayer;
}
