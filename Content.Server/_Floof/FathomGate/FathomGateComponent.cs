namespace Content.Server._Floof.FathomGate;

/// <summary>
/// One end of a fathom gate link.
/// Pairing the two ends pairs their anchored children by index.
/// </summary>
[RegisterComponent, Access(typeof(FathomGateSystem))]
public sealed partial class FathomGateComponent : Component;
