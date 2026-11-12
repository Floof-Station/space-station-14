using System.Numerics;
using Content.Shared._Floof.Link;
using Robust.Client.GameObjects;

namespace Content.Client._Floof.Link;

public sealed partial class StaticPortalSystem : SharedStaticPortalSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private PointLightSystem _light = default!;

    // prototype light radius, the light is client-side so we scale from this
    private readonly Dictionary<EntityUid, float> _baseRadius = new();

    [SubscribeLocalEvent]
    private void OnStartup(Entity<StaticPortalComponent> ent, ref ComponentStartup args)
    {
        if (_light.TryGetLight(ent, out var light))
            _baseRadius[ent] = light.Radius;

        UpdateVisuals(ent);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<StaticPortalComponent> ent, ref ComponentShutdown args)
    {
        _baseRadius.Remove(ent);
    }

    [SubscribeLocalEvent]
    private void OnHandleState(Entity<StaticPortalComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(Entity<StaticPortalComponent> ent)
    {
        var open = ent.Comp.Open > 0f;
        var scale = ent.Comp.MinScale + (1f - ent.Comp.MinScale) * ent.Comp.Open;

        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            _sprite.SetVisible((ent, sprite), open);
            _sprite.SetScale((ent, sprite), new Vector2(scale));
        }

        if (!_light.TryGetLight(ent, out var light))
            return;

        _light.SetEnabled(ent, open, light);
        if (_baseRadius.TryGetValue(ent, out var radius))
            _light.SetRadius(ent, radius * scale, light);
    }
}
