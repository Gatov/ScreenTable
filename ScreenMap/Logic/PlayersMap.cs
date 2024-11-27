using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ScreenMap.Logic.Messages;
namespace ScreenMap.Logic;

public class PlayersMap : IDisposable
{
    private Bitmap _playersImage;
    private Image _originalMap;
    private TextureBrush _revealBrush;
    private TextureBrush _semiRevealBrush;
    public event Action<RectangleF> OnRectUpdated;
    public event Action<IMessage> OnMessage;
    private MapInfo _mapInfo = new MapInfo();
    private SolidBrush _hideBrush;
    private float _zoomFactor = 1; 
    private bool _updateRect = false;
    private PointF _centerUnscaled = new PointF(200,200);
    private readonly Color _gridColor = Color.FromArgb(128,Color.Yellow);
    private float _lastDpiX = 90;
    private float _lastDpiY = 90;
    private SizeF _lastClientSize = new SizeF(100,100);


    public void Initialize(Stream mapStream)
    {
        _originalMap = Image.FromStream(mapStream);
        InitializePlayerImage(_originalMap);
        InitializeBrushes(_originalMap);
        NotifyUpdate();
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
        using var brush = new SolidBrush( Color.CadetBlue);
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
        //Rectangle clipRect = g.ClipBounds; 
        
        var rectInOriginal = GetViewAreaInOriginal(); 
        g.ScaleTransform(ZoomX, ZoomY);
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
            using var pen = new Pen(Color.FromArgb(200, markColor), 1f/ZoomX);
            g.DrawEllipse(pen, new PointF(mark.X, mark.Y).RectByCenter(mark.Radius));
        }
        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / ZoomY)))
        {
            for (float i = _mapInfo.OffsetX; i < rectInOriginal.X + rectInOriginal.Width; i += _mapInfo.CellSize)
                g.DrawLine(pen, i, rectInOriginal.Y, i, rectInOriginal.Y + rectInOriginal.Height);
        }

        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / ZoomX)))
        {
            for (float i = _mapInfo.OffsetY; i < rectInOriginal.Y + rectInOriginal.Height; i += _mapInfo.CellSize)
                g.DrawLine(pen, rectInOriginal.X, i, rectInOriginal.X + rectInOriginal.Width, i);
        }
        
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
        _revealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.8f);
        _semiRevealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.2f);
        _hideBrush = new SolidBrush( Color.CadetBlue);
    }

    public void Dispose()
    {
        _playersImage?.Dispose();
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
        _mapInfo.OffsetY = _mapInfo.OffsetY;
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