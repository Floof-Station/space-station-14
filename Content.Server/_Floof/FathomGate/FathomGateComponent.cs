namespace Content.Server._Floof.FathomGate;

[RegisterComponent, Access(typeof(FathomGateSystem))]
public sealed partial class FathomGateComponent : Component
{
    [DataField]
    public FathomGateState State = FathomGateState.Closed;
}

public enum FathomGateState : byte
{
    Closed,
    Open,
    Collapsed,
}
