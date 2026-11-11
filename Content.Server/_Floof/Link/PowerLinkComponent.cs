namespace Content.Server._Floof.Link;

[RegisterComponent, Access(typeof(PowerLinkSystem))]
public sealed partial class PowerLinkComponent : Component
{
    [DataField(required: true)]
    public PowerLinkDirection Direction;
    
    /// Drawn on top of the power networks demand so it can ramp up. Input end only.
    [DataField]
    public float Headroom = 2_000f;
}

public enum PowerLinkDirection : byte
{
    Input,
    Output,
}
