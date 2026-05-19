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
    private DetectionStore _detectionStore;
    private Func<bool> _showDetections;
    public string Name { get; private set; }

    public void SetDetectionOverlay(DetectionStore store, Func<bool> showFlag)
    {
        _detectionStore = store;
        _showDetections = showFlag;
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
        RenderToGraphics(g, _lastDpiX, _lastDpiY, clientSize);
    }

    private void RenderToGraphics(Graphics g, float dpiX, float dpiY, SizeF clientSize)
    {
        if (_playersImage == null) return;
        if (_mapInfo.CellSize <= 0)
        {
            g.DrawImage(_playersImage, 0, 0);
            DrawCornerFiducials(g, clientSize);
            return;
        }

        float zoomX = dpiX / _mapInfo.CellSize * _zoomFactor;
        float zoomY = dpiY / _mapInfo.CellSize * _zoomFactor;
        float viewWidth = clientSize.Width / zoomX;
        float viewHeight = clientSize.Height / zoomY;
        var rectInOriginal = new RectangleF(
            _centerUnscaled.X - viewWidth / 2,
            _centerUnscaled.Y - viewHeight / 2,
            viewWidth, viewHeight);

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
        DrawDetectionOverlay(g, zoomX);
        g.ResetTransform();
        DrawCornerFiducials(g, clientSize);
    }

    private void DrawDetectionOverlay(Graphics g, float zoomX)
    {
        if (_detectionStore == null || _showDetections == null || !_showDetections()) return;
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
        if (w < FiducialSizePx * 2 || h < FiducialSizePx * 2) return;
        var markers = ArucoMarkers.GetMarkers(FiducialSizePx);
        var prevInterp = g.InterpolationMode;
        var prevComp = g.CompositingMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        // White quiet zone keeps ArUco detection reliable even on dark/textured map content.
        using (var white = new SolidBrush(Color.White))
        {
            int q = FiducialSizePx + 8;
            g.FillRectangle(white, 0, 0, q, q);
            g.FillRectangle(white, w - q, 0, q, q);
            g.FillRectangle(white, w - q, h - q, q, q);
            g.FillRectangle(white, 0, h - q, q, q);
        }
        g.DrawImage(markers[0], 4, 4, FiducialSizePx, FiducialSizePx);
        g.DrawImage(markers[1], w - FiducialSizePx - 4, 4, FiducialSizePx, FiducialSizePx);
        g.DrawImage(markers[2], w - FiducialSizePx - 4, h - FiducialSizePx - 4, FiducialSizePx, FiducialSizePx);
        g.DrawImage(markers[3], 4, h - FiducialSizePx - 4, FiducialSizePx, FiducialSizePx);
        g.InterpolationMode = prevInterp;
        g.CompositingMode = prevComp;
    }

    public Bitmap RenderSnapshot(Size size)
    {
        if (_playersImage == null) return null;
        var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Black);
        RenderToGraphics(g, 96f, 96f, size);
        return bitmap;
    }

    /// <summary>
    /// Returns the rectangle (in unscaled map coordinates) that would be rendered
    /// when calling RenderSnapshot at the given size. Returns Empty if no map is loaded
    /// or CellSize is invalid.
    /// </summary>
    public RectangleF GetSnapshotViewRect(Size size)
    {
        if (_playersImage == null || _mapInfo.CellSize <= 0) return RectangleF.Empty;
        float zoomX = 96f / _mapInfo.CellSize * _zoomFactor;
        float zoomY = 96f / _mapInfo.CellSize * _zoomFactor;
        float viewWidth = size.Width / zoomX;
        float viewHeight = size.Height / zoomY;
        return new RectangleF(
            _centerUnscaled.X - viewWidth / 2,
            _centerUnscaled.Y - viewHeight / 2,
            viewWidth, viewHeight);
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