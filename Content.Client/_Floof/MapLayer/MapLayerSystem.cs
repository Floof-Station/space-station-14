using System.Diagnostics.CodeAnalysis;
using Content.Shared._Floof.MapLayer;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Floof.MapLayer;

public readonly record struct MapLayerView(IEye Eye, float Gap, float Alpha);

public sealed partial class MapLayerSystem : SharedMapLayerSystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedRoofSystem _roof = default!;

    public const float ViewHeight = 20f;

    private const float MinZoom = 0.05f;

    private readonly List<(EntityUid Map, float Gap)> _gaps = new();
    private IClydeViewport? _drawing;
    private IClydeViewport? _drawingOther;
    private float _drawingOtherGap;
    private IClydeViewport? _aboveViewport;
    private Texture? _aboveTexture;
    private float _aboveAlpha;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new MapLayerAboveOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<MapLayerAboveOverlay>();
    }

    public bool TryGetViews(IEye eye, List<MapLayerView> below, out MapLayerView? above)
    {
        below.Clear();
        above = null;

        if (_map.GetMapOrInvalid(eye.Position.MapId) is not { Valid: true } own ||
            !TryGetGaps(own, _gaps) ||
            !TryGetVisibility(own, out var belowRange, out var aboveFade, IsUnderRoof(eye.Position)))
            return false;

        foreach (var (map, gap) in _gaps)
        {
            if (map == own)
                continue;

            if (gap > 0f)
            {
                if (gap <= belowRange)
                    below.Add(new MapLayerView(MakeEye(eye, map, GetZoom(gap)), gap, 1f));

                continue;
            }

            if (-gap < aboveFade)
            {
                var alpha = 1f + gap / aboveFade;
                above = new MapLayerView(MakeEye(eye, map, GetZoom(gap)), gap, alpha * alpha);
            }

            break;
        }

        return below.Count > 0 || above != null;
    }

    public static float GetZoom(float gap)
    {
        return MathF.Max((ViewHeight + gap) / ViewHeight, MinZoom);
    }

    public void SetDrawingOver(IClydeViewport? viewport)
    {
        _drawing = viewport;
    }

    public bool IsDrawingOver(IClydeViewport viewport)
    {
        return viewport == _drawing;
    }

    public void SetDrawingOtherLayer(IClydeViewport? viewport, float gap = 0f)
    {
        _drawingOther = viewport;
        _drawingOtherGap = gap;
    }

    public bool IsDrawingOtherLayer(IClydeViewport viewport)
    {
        return viewport == _drawingOther;
    }

    public bool TryGetOtherLayerGap(IClydeViewport viewport, out float gap)
    {
        gap = _drawingOtherGap;
        return viewport == _drawingOther;
    }

    public void SetDrawnAbove(IClydeViewport? viewport, Texture? texture = null, float alpha = 0f)
    {
        _aboveViewport = viewport;
        _aboveTexture = texture;
        _aboveAlpha = alpha;
    }

    public bool TryGetDrawnAbove(IClydeViewport viewport, [NotNullWhen(true)] out Texture? texture, out float alpha)
    {
        texture = _aboveTexture;
        alpha = _aboveAlpha;
        return viewport == _aboveViewport && texture != null;
    }

    private bool IsUnderRoof(MapCoordinates position)
    {
        if (!_map.TryFindGridAt(position, out var gridUid, out var grid))
            return false;

        if (HasComp<ImplicitRoofComponent>(gridUid))
            return true;

        return TryComp<RoofComponent>(gridUid, out var roof) &&
               _roof.IsRooved((gridUid, grid, roof), _map.TileIndicesFor(gridUid, grid, position));
    }

    private IEye MakeEye(IEye eye, EntityUid map, float zoom)
    {
        return new Robust.Shared.Graphics.Eye
        {
            Position = new MapCoordinates(eye.Position.Position, Comp<MapComponent>(map).MapId),
            Rotation = eye.Rotation,
            Offset = eye.Offset,
            Zoom = eye.Zoom * zoom,
            DrawFov = false,
            DrawLight = eye.DrawLight,
        };
    }
}
