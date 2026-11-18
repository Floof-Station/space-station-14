using Content.Client._Floof.MapLayer;
using Robust.Client.Graphics;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private const string PlacementOverlayType = "Robust.Client.Placement.PlacementManager+PlacementOverlay";

    private MapLayerSystem? _layers;
    private Overlay? _placementOverlay;
    private IClydeViewport? _layerAbove;
    private readonly List<MapLayerView> _layersBelow = new();

    private bool RenderWithLayers(IClydeViewport viewport)
    {
        _layers ??= _entityManager.SystemOrNull<MapLayerSystem>();
        if (_eye is not { } eye ||
            _layers == null ||
            !_layers.TryGetViews(eye, _layersBelow, out var above))
        {
            FreeLayerAbove();
            return false;
        }

        var clear = viewport.ClearColor;
        try
        {
            var placement = HidePlacementOverlay();
            try
            {
                if (above is { } aboveView)
                    RenderLayerAbove(viewport, aboveView);
                else
                    FreeLayerAbove();

                RenderLayersBelow(viewport);
            }
            finally
            {
                if (placement != null)
                    _overlayManager.AddOverlay(placement);
            }

            viewport.Eye = eye;
            var over = _layersBelow.Count > 0;
            if (over)
            {
                viewport.ClearColor = null;
                _layers.SetDrawingOver(viewport);
            }

            if (above is { } view && _layerAbove != null)
                _layers.SetDrawnAbove(viewport, _layerAbove.RenderTarget.Texture, view.Alpha);

            try
            {
                viewport.Render();
            }
            finally
            {
                _layers.SetDrawingOver(null);
                _layers.SetDrawnAbove(null);
            }
        }
        finally
        {
            viewport.Eye = eye;
            viewport.ClearColor = clear;
        }

        return true;
    }

    private void RenderLayersBelow(IClydeViewport viewport)
    {
        var clear = viewport.ClearColor;
        var first = true;
        foreach (var layer in _layersBelow)
        {
            viewport.Eye = layer.Eye;
            viewport.ClearColor = first ? clear : null;
            if (!first)
                _layers!.SetDrawingOver(viewport);

            _layers!.SetDrawingOtherLayer(viewport, layer.Gap);
            try
            {
                viewport.Render();
            }
            finally
            {
                _layers.SetDrawingOver(null);
                _layers.SetDrawingOtherLayer(null);
            }

            first = false;
        }
    }

    private void RenderLayerAbove(IClydeViewport viewport, MapLayerView above)
    {
        if (_layerAbove == null || _layerAbove.Size != viewport.Size)
        {
            FreeLayerAbove();
            _layerAbove = _clyde.CreateViewport(viewport.Size, "MapLayerAbove");
        }

        _layerAbove.RenderScale = viewport.RenderScale;
        _layerAbove.Eye = above.Eye;
        _layerAbove.ClearColor = Color.Transparent;
        _layers!.SetDrawingOver(_layerAbove);
        _layers.SetDrawingOtherLayer(_layerAbove, above.Gap);
        try
        {
            _layerAbove.Render();
        }
        finally
        {
            _layers.SetDrawingOver(null);
            _layers.SetDrawingOtherLayer(null);
        }
    }

    private Overlay? HidePlacementOverlay()
    {
        if (_placementOverlay == null)
        {
            foreach (var overlay in _overlayManager.AllOverlays)
            {
                if (overlay.GetType().FullName != PlacementOverlayType)
                    continue;

                _placementOverlay = overlay;
                break;
            }
        }

        return _placementOverlay != null && _overlayManager.RemoveOverlay(_placementOverlay) ? _placementOverlay : null;
    }

    private void FreeLayerAbove()
    {
        _layerAbove?.Dispose();
        _layerAbove = null;
    }
}
