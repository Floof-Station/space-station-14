namespace Content.Server._Floof.Expo;

/// <summary>
/// One end of the Expo link, on both the anchor and the core.
/// Pairing the two ends pairs their anchored children by index.
/// </summary>
[RegisterComponent, Access(typeof(ExpoLinkSystem))]
public sealed partial class ExpoLinkComponent : Component;
