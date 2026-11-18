using JetBrains.Annotations;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._Floof.MapLayer;

public abstract partial class SharedMapLayerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    [PublicAPI]
    public void SetLayer(EntityUid map, int stack, float z)
    {
        var layer = EnsureComp<MapLayerComponent>(map);
        layer.Stack = stack;
        layer.Z = z;
        Dirty(map, layer);
    }

    [PublicAPI]
    public void StartTransition(EntityUid map,
        int stack,
        float startZ,
        float endZ,
        TimeSpan duration,
        MapLayerEasing easing = MapLayerEasing.InOutSine)
    {
        SetLayer(map, stack, endZ);

        var transition = EnsureComp<MapLayerTransitionComponent>(map);
        transition.StartZ = startZ;
        transition.EndZ = endZ;
        transition.StartTime = _timing.CurTime;
        transition.Duration = duration;
        transition.Easing = easing;
        Dirty(map, transition);
    }

    [PublicAPI]
    public void RemoveLayer(EntityUid map)
    {
        RemComp<MapLayerTransitionComponent>(map);
        RemComp<MapLayerComponent>(map);
    }

    [PublicAPI]
    public bool TryGetZ(Entity<MapLayerComponent?> layer, out float z)
    {
        z = 0f;
        if (!Resolve(layer, ref layer.Comp, false))
            return false;

        z = TryComp<MapLayerTransitionComponent>(layer, out var transition)
            ? GetTransitionZ(transition)
            : layer.Comp.Z;
        return true;
    }

    [PublicAPI]
    public bool TryGetStack(EntityUid uid, out int stack)
    {
        stack = 0;
        if (Transform(uid).MapUid is not { } map || !TryComp<MapLayerComponent>(map, out var layer))
            return false;

        stack = layer.Stack;
        return true;
    }

    [PublicAPI]
    public bool TryGetVisibility(EntityUid uid, out float belowRange, out float aboveFade, bool roofed = false)
    {
        belowRange = 0f;
        aboveFade = 0f;
        if (Transform(uid).MapUid is not { } map || !TryComp<MapLayerComponent>(map, out var layer))
            return false;

        belowRange = layer.BelowRange;
        aboveFade = roofed ? layer.RoofedAboveFade : layer.AboveFade;
        return true;
    }

    [PublicAPI]
    public bool TryGetRoof(EntityUid map, out MapLayerRoof roof)
    {
        roof = default;
        if (!TryComp<MapLayerComponent>(map, out var layer))
            return false;

        roof = new MapLayerRoof(layer.RoofFadeStart, layer.RoofFadeEnd, layer.RoofTexture, layer.RoofColor);
        return true;
    }

    [PublicAPI]
    public void GetLayers(int stack, List<(EntityUid Map, float Z)> layers)
    {
        layers.Clear();
        var query = EntityQueryEnumerator<MapLayerComponent>();
        while (query.MoveNext(out var uid, out var layer))
        {
            if (layer.Stack == stack && TryGetZ((uid, layer), out var z))
                layers.Add((uid, z));
        }

        layers.Sort((a, b) => a.Z.CompareTo(b.Z));
    }

    [PublicAPI]
    public bool TryGetGaps(EntityUid map, List<(EntityUid Map, float Gap)> gaps)
    {
        gaps.Clear();
        if (!TryGetStack(map, out var stack) || !TryGetZ(map, out var z))
            return false;

        GetLayers(stack, gaps);
        for (var i = 0; i < gaps.Count; i++)
        {
            gaps[i] = (gaps[i].Map, z - gaps[i].Gap);
        }

        return true;
    }

    private float GetTransitionZ(MapLayerTransitionComponent transition)
    {
        var progress = transition.Duration > TimeSpan.Zero
            ? Math.Clamp((float) ((_timing.CurTime - transition.StartTime) / transition.Duration), 0f, 1f)
            : 1f;

        var eased = transition.Easing switch
        {
            MapLayerEasing.InOutSine => Easings.InOutSine(progress),
            MapLayerEasing.InOutCubic => Easings.InOutCubic(progress),
            MapLayerEasing.InSine => Easings.InSine(progress),
            MapLayerEasing.OutSine => Easings.OutSine(progress),
            _ => progress,
        };

        return transition.StartZ + (transition.EndZ - transition.StartZ) * eased;
    }
}

public readonly record struct MapLayerRoof(float FadeStart, float FadeEnd, ResPath Texture, Color Color)
{
    public float GetAlpha(float gap)
    {
        if (FadeEnd <= FadeStart)
            return gap >= FadeEnd ? 1f : 0f;

        return Math.Clamp((gap - FadeStart) / (FadeEnd - FadeStart), 0f, 1f);
    }
}
