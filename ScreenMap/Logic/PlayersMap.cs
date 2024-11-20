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


    public void Initialize(Stream mapStream)
    {
        _originalMap = Image.FromStream(mapStream);
        InitializePlayerImage(_originalMap);
        InitializeBrushes(_originalMap);
        OnRectUpdated?.Invoke(RectangleF.Empty);
    }
    private void InitializePlayerImage(Image newMap)
    {
        _playersImage = new Bitmap(newMap.Width, newMap.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(_playersImage);
        using var brush = new SolidBrush( Color.CadetBlue);
        g.FillRectangle(brush, 0,0, _playersImage.Width, _playersImage.Height);
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
        OnRectUpdated?.Invoke(rect);
    }

    public void OnPaint(Graphics g, SizeF clientSize)
    {
        var zoomX = g.DpiX/_mapInfo.CellSize * _zoomFactor;
        var zoomY = g.DpiY/_mapInfo.CellSize * _zoomFactor;
        //Rectangle clipRect = g.ClipBounds; 
        
        var rectInOriginal = GetViewAreaInOriginal(clientSize, zoomX, zoomY); 
        g.ScaleTransform(zoomX, zoomY);
        g.TranslateTransform(-rectInOriginal.X, -rectInOriginal.Y);
        
        
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_playersImage, rectInOriginal, rectInOriginal, GraphicsUnit.Pixel);
        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / zoomY)))
        {
            for (float i = _mapInfo.OffsetX; i < rectInOriginal.X + rectInOriginal.Width; i += _mapInfo.CellSize)
                g.DrawLine(pen, i, rectInOriginal.Y, i, rectInOriginal.Y + rectInOriginal.Height);
        }

        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / zoomX)))
        {
            for (float i = _mapInfo.OffsetY; i < rectInOriginal.Y + rectInOriginal.Height; i += _mapInfo.CellSize)
                g.DrawLine(pen, rectInOriginal.X, i, rectInOriginal.X + rectInOriginal.Width, i);
        }
    }

    private RectangleF GetViewAreaInOriginal(SizeF clientSize, float zoomX, float zoomY)
    {
        var width = clientSize.Width/zoomX;
        var height = clientSize.Height/zoomY;
        return new RectangleF(_centerUnscaled.X - width / 2, _centerUnscaled.Y - height / 2, width, height);
    }

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
        OnRectUpdated?.Invoke(RectangleF.Empty);
    }

    public void UpdateVisibleArea(int clientSizeWidth, int clientSizeHeight)
    {
        
    }

    public void UpdateGrid(GridDataMessage gridData)
    {
        _mapInfo.CellSize = gridData.CellSize;
        _mapInfo.OffsetX = gridData.OffsetX;
        _mapInfo.OffsetY = _mapInfo.OffsetY;
    }
}