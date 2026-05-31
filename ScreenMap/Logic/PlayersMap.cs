using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ScreenMap.Logic.Camera;
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
    private const int FiducialSizePx = 80;
    private const int FiducialQuietPx = 12; // white quiet zone around the marker
    private const int FiducialRingPx = 8;   // black ring around the quiet zone
    // Marker-center inset as a fraction of the view. Must be large enough that the black
    // square stays on-screen at the detector reference size (960x540) without clamping —
    // otherwise the fraction differs between the filmed screen and the reference and the
    // map misaligns. 0.12*540 = 64.8 > half the black square (60).
    private const float FiducialInsetFrac = 0.12f;
    private DetectionStore _detectionStore;
    public string Name { get; private set; }

    public void SetDetectionOverlay(DetectionStore store)
    {
        _detectionStore = store;
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
        var dets = _detectionStore.Snapshot();
        if (dets.Length == 0) return;
        var prev = g.CompositingMode;
        g.CompositingMode = CompositingMode.SourceOver;
        using var fill = new SolidBrush(Color.FromArgb(80, Color.LimeGreen));
        using var pen = new Pen(Color.FromArgb(220, Color.LimeGreen), Math.Max(1f / zoomX, 0.5f));
        foreach (var d in dets)
        {
            var r = new RectangleF(d.Center.X - d.Radius, d.Center.Y - d.Radius, d.Radius * 2, d.Radius * 2);
            g.FillEllipse(fill, r);
            g.DrawEllipse(pen, r);
        }
        g.CompositingMode = prev;
    }

    private static void DrawCornerFiducials(Graphics g, SizeF clientSize)
    {
        int w = (int)clientSize.Width;
        int h = (int)clientSize.Height;
        int corner = FiducialSizePx + 2 * (FiducialQuietPx + FiducialRingPx);
        if (w < corner * 2 || h < corner * 2) return;
        var markers = ArucoMarkers.GetMarkers(FiducialSizePx);
        var prevInterp = g.InterpolationMode;
        var prevComp = g.CompositingMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;

        // Each fiducial gets a black ring around a white quiet zone. The white zone alone
        // vanishes against bright map content (and a black marker border washes out under
        // glare); the black ring gives a brightness-independent boundary so the detector
        // can localize the quiet zone on any map.
        const int quiet = FiducialQuietPx;
        const int ring = FiducialRingPx;
        int white = FiducialSizePx + 2 * quiet;
        int black = white + 2 * ring;
        int half = black / 2;

        // Marker CENTERS sit at a fixed FRACTION of the view, so they land on the same map
        // world-point regardless of render resolution (the filmed screen vs the lower-res
        // detector reference). The detector aligns by mapping these centers to each other,
        // so they must be resolution-independent. Markers stay a fixed pixel size (the
        // camera needs the pixels to decode them); only their position is fractional.
        int fx = Math.Clamp((int)Math.Round(FiducialInsetFrac * w), half, w - half);
        int fy = Math.Clamp((int)Math.Round(FiducialInsetFrac * h), half, h - half);

        void DrawCentered(int cx, int cy, Bitmap marker)
        {
            g.FillRectangle(Brushes.Black, cx - half, cy - half, black, black);
            g.FillRectangle(Brushes.White, cx - half + ring, cy - half + ring, white, white);
            g.DrawImage(marker, cx - FiducialSizePx / 2, cy - FiducialSizePx / 2, FiducialSizePx, FiducialSizePx);
        }
        DrawCentered(fx, fy, markers[0]);
        DrawCentered(w - fx, fy, markers[1]);
        DrawCentered(w - fx, h - fy, markers[2]);
        DrawCentered(fx, h - fy, markers[3]);

        g.InterpolationMode = prevInterp;
        g.CompositingMode = prevComp;
    }

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