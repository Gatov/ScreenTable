using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ScreenMap.Vision;
using ScreenMap.Logic.Messages;
namespace ScreenMap.Logic;

public class PlayersMap : IDisposable
{
    private Bitmap _playersImage;
    private Image _originalMap;
    private TextureBrush _revealBrush;
    private Bitmap _revealTexture;
    private TextureBrush _semiRevealBrush;
    private Bitmap _semiRevealTexture;
    public event Action<RectangleF> OnRectUpdated;
    public event Action<MapMessage> OnMessage;
    private MapInfo _mapInfo = new MapInfo();
    private SolidBrush _hideBrush;
    private float _zoomFactor = 1; 
    private bool _updateRect = false;
    private PointF _centerUnscaled = new PointF(200,200);
    private readonly Color _gridColor = Color.FromArgb(128,Color.Yellow);
    private float _lastDpiX = 90;
    private float _lastDpiY = 90;
    private SizeF _lastClientSize = new SizeF(100,100);
    private Color _fogOfWar = Color.Tan;
    private bool _showGrid = true;

    private DetectionStore _detectionStore;
    private Func<bool> _showFigurines;
    public string Name { get; private set; }

    public void SetDetectionOverlay(DetectionStore store, Func<bool> showFigurines)
    {
        _detectionStore = store;
        _showFigurines = showFigurines;
    }


    public void Initialize(Stream mapStream, string name)
    {
        _playersImage?.Dispose();
        _revealBrush?.Dispose();
        _revealTexture?.Dispose();
        _semiRevealBrush?.Dispose();
        _semiRevealTexture?.Dispose();
        _hideBrush?.Dispose();
        _originalMap?.Dispose();

        _originalMap = Image.FromStream(mapStream);
        InitializePlayerImage(_originalMap);
        InitializeBrushes(_originalMap);
        NotifyUpdate();
        Name = name;

    }

    private void NotifyUpdate(RectangleF? rc = null)
    {
        if(rc ==null)
            OnRectUpdated?.Invoke(RectangleF.Empty);
        else
        {
            var rectInOriginal = GetViewAreaInOriginal();
            var translated = rc.Value.Translate(-rectInOriginal.X, -rectInOriginal.Y);
            var scaledRect = translated.Scale(ZoomX, ZoomY);
            OnRectUpdated?.Invoke(scaledRect);
        }
    }
    

    private void InitializePlayerImage(Image newMap)
    {
        _playersImage = new Bitmap(newMap.Width, newMap.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(_playersImage);
        using var brush = new SolidBrush( _fogOfWar);
        g.FillRectangle(brush, 0,0, _playersImage.Width, _playersImage.Height);
        NotifyUpdate();
    }
    
    public void RevealAt(PointF unscaledPoint, float brushSize, bool revealAtReveal)
    {
        var rect = new RectangleF(unscaledPoint.X - brushSize / 2f, unscaledPoint.Y - brushSize / 2f, brushSize, brushSize);
        // Reveal at player's view
        //var rect = new RectangleF(unscaledPoint.X - _brushSize / 2f, unscaledPoint.Y - _brushSize / 2f, _brushSize, _brushSize);
        using (Graphics g = Graphics.FromImage(_playersImage))
        {
            g.CompositingMode = CompositingMode.SourceOver;
            // set the _revealBrush to draw a portion of original image in place
            var bigger = rect;
            bigger.Inflate(0.2f * brushSize, 0.2f * brushSize);
            if (revealAtReveal)
            {
                g.FillEllipse(_semiRevealBrush, bigger);
                g.FillEllipse(_revealBrush, rect);
            }
            else
            {
                g.FillEllipse(_hideBrush, bigger);
            }
        }

        NotifyUpdate(rect);
    }

    public void OnPaint(Graphics g, SizeF clientSize)
    {
        _lastDpiX = g.DpiX;
        _lastDpiY = g.DpiY;
        _lastClientSize = clientSize;
        // The player screen is what the detection camera films — it must NEVER show the
        // detection overlay, or the circles get filmed back in and pollute detection.
        // The overlay is added only off-screen for the web/GM (RenderSnapshot).
        var worldRect = _mapInfo.CellSize > 0
            ? ComputeViewRect(clientSize, g.DpiX, g.DpiY)
            : RectangleF.Empty;
        RenderToGraphics(g, worldRect, clientSize, includeDetections: false);
    }

    /// <summary>The map rectangle (in unscaled map pixels) currently shown on the player
    /// screen for the given client size and DPI. Depends only on zoom/center, so any
    /// output resolution that renders this same rect shows the identical map extent.</summary>
    private RectangleF ComputeViewRect(SizeF clientSize, float dpiX, float dpiY)
    {
        float zoomX = dpiX / _mapInfo.CellSize * _zoomFactor;
        float zoomY = dpiY / _mapInfo.CellSize * _zoomFactor;
        float viewWidth = clientSize.Width / zoomX;
        float viewHeight = clientSize.Height / zoomY;
        return new RectangleF(
            _centerUnscaled.X - viewWidth / 2,
            _centerUnscaled.Y - viewHeight / 2,
            viewWidth, viewHeight);
    }

    // Renders the given world rect into an output of the given pixel size. The output
    // resolution is decoupled from the map extent, so a low-res snapshot shows exactly the
    // same region as the full-res player screen — essential for the detector to align the
    // warped camera frame against this reference.
    private void RenderToGraphics(Graphics g, RectangleF worldRect, SizeF outputSize, bool includeDetections)
    {
        if (_playersImage == null) return;
        if (_mapInfo.CellSize <= 0)
        {
            g.DrawImage(_playersImage, 0, 0);
            DrawCornerFiducials(g, outputSize);
            return;
        }

        float zoomX = outputSize.Width / worldRect.Width;
        float zoomY = outputSize.Height / worldRect.Height;
        var rectInOriginal = worldRect;

        g.ScaleTransform(zoomX, zoomY);
        g.TranslateTransform(-rectInOriginal.X, -rectInOriginal.Y);
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_playersImage, rectInOriginal, rectInOriginal, GraphicsUnit.Pixel);
        foreach (var mark in _mapInfo.Marks)
        {
            var markColor = Color.FromArgb(mark.ArgbColor);
            using var brush = new SolidBrush(markColor);
            g.FillEllipse(brush, new PointF(mark.X, mark.Y).RectByCenter(mark.Radius));
            using var pen = new Pen(Color.FromArgb(200, markColor), 1f / zoomX);
            g.DrawEllipse(pen, new PointF(mark.X, mark.Y).RectByCenter(mark.Radius));
        }
        if (_showGrid && _mapInfo.CellSize > 0)
        {
            using (var pen = new Pen(_gridColor, Math.Min(1.5f, 3f / zoomY)))
            {
                for (float i = _mapInfo.OffsetX; i < rectInOriginal.X + rectInOriginal.Width; i += _mapInfo.CellSize)
                    g.DrawLine(pen, i, rectInOriginal.Y, i, rectInOriginal.Y + rectInOriginal.Height);
            }
            using (var pen = new Pen(_gridColor, Math.Min(1.5f, 3f / zoomX)))
            {
                for (float i = _mapInfo.OffsetY; i < rectInOriginal.Y + rectInOriginal.Height; i += _mapInfo.CellSize)
                    g.DrawLine(pen, rectInOriginal.X, i, rectInOriginal.X + rectInOriginal.Width, i);
            }
        }
        if (includeDetections) DrawDetectionOverlay(g, zoomX);
        g.ResetTransform();
        DrawCornerFiducials(g, outputSize);
    }

    private void DrawDetectionOverlay(Graphics g, float zoomX)
    {
        if (_detectionStore == null) return;
        var (dets, crops) = _detectionStore.SnapshotFrame();
        if (dets.Length == 0) return;
        bool figs = _showFigurines != null && _showFigurines();
        var prev = g.CompositingMode;
        var prevInterp = g.InterpolationMode;
        g.CompositingMode = CompositingMode.SourceOver;
        using var fill = new SolidBrush(Color.FromArgb(80, Color.LimeGreen));
        using var pen = new Pen(Color.FromArgb(220, Color.LimeGreen), Math.Max(1f / zoomX, 0.5f));
        for (int i = 0; i < dets.Length; i++)
        {
            var d = dets[i];
            var r = new RectangleF(d.Center.X - d.Radius, d.Center.Y - d.Radius, d.Radius * 2, d.Radius * 2);
            var crop = figs && i < crops.Length ? crops[i] : null;
            if (crop != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(crop, r);
            }
            else
            {
                g.FillEllipse(fill, r);
                g.DrawEllipse(pen, r);
            }
        }
        g.InterpolationMode = prevInterp;
        g.CompositingMode = prev;
    }

    private static void DrawCornerFiducials(Graphics g, SizeF clientSize)
        => MarkerRenderer.DrawCornerFiducials(g, clientSize);

    public Bitmap RenderSnapshot(Size size, bool includeDetections = true)
    {
        if (_playersImage == null) return null;
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Black);
        // Render the SAME map extent the player screen currently shows, scaled into this
        // bitmap — so the detector's reference aligns with the warped camera frame.
        var worldRect = _mapInfo.CellSize > 0 ? LiveViewRect(size) : RectangleF.Empty;
        RenderToGraphics(g, worldRect, size, includeDetections);
        return bitmap;
    }

    /// <summary>The world rect shown on the player screen (from its last paint), falling
    /// back to the requested size at 96 DPI if the player view has not painted yet.</summary>
    private RectangleF LiveViewRect(Size fallback)
    {
        var client = _lastClientSize.Width > 0 ? _lastClientSize : new SizeF(fallback.Width, fallback.Height);
        float dpiX = _lastDpiX > 0 ? _lastDpiX : 96f;
        float dpiY = _lastDpiY > 0 ? _lastDpiY : 96f;
        return ComputeViewRect(client, dpiX, dpiY);
    }

    /// <summary>
    /// Returns the rectangle (in unscaled map coordinates) that would be rendered
    /// when calling RenderSnapshot at the given size. Returns Empty if no map is loaded
    /// or CellSize is invalid.
    /// </summary>
    public RectangleF GetSnapshotViewRect(Size size)
    {
        if (_playersImage == null || _mapInfo.CellSize <= 0) return RectangleF.Empty;
        // Must match what RenderSnapshot renders, so detections translate back correctly.
        return LiveViewRect(size);
    }

    /// <summary>Detection-space pixels for one grid cell (one 2.5 cm token) in a snapshot of the
    /// given size — i.e. CellSize scaled by the snapshot's zoom. Returns 0 when no map/grid, so the
    /// detector falls back to pixel-area sizing. Averages the two axes (they differ only under
    /// anisotropic DPI).</summary>
    public float PixelsPerCell(Size snapshotSize)
    {
        var (x, y) = PixelsPerCellXY(snapshotSize);
        return (x + y) / 2f;
    }

    /// <summary>TEMP DIAGNOSTIC: per-axis pixels-per-cell. They diverge when the snapshot aspect
    /// differs from the live player-view aspect (the snapshot then renders cells non-square).</summary>
    public (float x, float y) PixelsPerCellXY(Size snapshotSize)
    {
        if (_playersImage == null || _mapInfo.CellSize <= 0) return (0f, 0f);
        var worldRect = LiveViewRect(snapshotSize);
        if (worldRect.Width <= 0 || worldRect.Height <= 0) return (0f, 0f);
        float zx = snapshotSize.Width / worldRect.Width;
        float zy = snapshotSize.Height / worldRect.Height;
        return (zx * _mapInfo.CellSize, zy * _mapInfo.CellSize);
    }

    public void SetGridVisible(bool show)
    {
        _showGrid = show;
        NotifyUpdate();
    }

    private RectangleF GetViewAreaInOriginal()
    {
        var width = _lastClientSize.Width/ZoomX;
        var height = _lastClientSize.Height/ZoomY;
        return new RectangleF(_centerUnscaled.X - width / 2, _centerUnscaled.Y - height / 2, width, height);
    }

    private float ZoomY => _lastDpiY/_mapInfo.CellSize * _zoomFactor;

    private float ZoomX => _lastDpiX/_mapInfo.CellSize * _zoomFactor;

    private void InitializeBrushes(Image newMap)
    {
        _revealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.8f, out _revealTexture);
        _semiRevealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.2f, out _semiRevealTexture);
        _hideBrush = new SolidBrush( _fogOfWar);
    }

    public void Dispose()
    {
        _playersImage?.Dispose();
        _revealBrush?.Dispose();
        _revealTexture?.Dispose();
        _semiRevealBrush?.Dispose();
        _semiRevealTexture?.Dispose();
        _hideBrush?.Dispose();
        _originalMap?.Dispose();
    }

    public void CenterAt(PointF centerAtLocation)
    {
        _centerUnscaled = centerAtLocation;
        NotifyUpdate();
        OnMessage?.Invoke(new ClientRectangleMessage(){Rectangle = GetViewAreaInOriginal()});
    }

    public void UpdateVisibleArea(int clientSizeWidth, int clientSizeHeight)
    {
        
    }

    public void UpdateGrid(GridDataMessage gridData)
    {
        _mapInfo.CellSize = gridData.CellSize;
        _mapInfo.OffsetX = gridData.OffsetX;
        _mapInfo.OffsetY = gridData.OffsetY;
        NotifyUpdate();
    }

    public void UpdateZoom(ZoomInMessage zoomMessage)
    {
        _zoomFactor = Math.Max(0.2f, Math.Min(5, _zoomFactor + zoomMessage.Ticks/10F)); // 10%
        NotifyUpdate();
        OnMessage?.Invoke(new ClientRectangleMessage(){Rectangle = GetViewAreaInOriginal()});
    }

    public void MarkAt(MarkAtMessage markAt)
    {
        if (markAt.Radius == 0) // remove
        {
            int idx = _mapInfo.Marks.FindLastIndex(x => x.Id == markAt.Id);
            if (idx >= 0)
            {
                var old = _mapInfo.Marks[idx];
                _mapInfo.Marks.RemoveAt(idx);
                NotifyUpdate(old.AsPoint.RectByCenter(old.Radius));
            }
        }
        else
        {
            var mark = new Mark()
            {
                Id = markAt.Id,
                Radius = markAt.Radius,
                X = markAt.Location.X,
                Y = markAt.Location.Y,
                ArgbColor = markAt.ArgbColor
            };
            _mapInfo.Marks.Add(mark);
            NotifyUpdate(mark.AsPoint.RectByCenter(mark.Radius));
        }
    }
}