namespace Content.Server._Floof.MapLayer.Station;

[RegisterComponent, Access(typeof(MapLayerSetupSystem))]
public sealed partial class MapLayerSetupComponent : Component
{
    [DataField(required: true)]
    public List<MapLayerEntry> Layers = new();
}
