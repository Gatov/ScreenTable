using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenMap.Logic;

public class PlayersMap : IDisposable
{
    
    public Image PlayersImage => _playersImage;
    private Bitmap _playersImage;
    private readonly Image _originalMap;
    private TextureBrush _revealBrush;
    private TextureBrush _semiRevealBrush;
    public event Action<RectangleF> OnRectUpdated;
    private MapInfo _mapInfo;

    
    public PlayersMap(Stream mapStream)
    {
        _originalMap = Image.FromStream(mapStream);
        InitializePlayerImage(_originalMap);
        InitializeBrushes(_originalMap);
        
    }
    private void InitializePlayerImage(Image newMap)
    {
        _playersImage = new Bitmap(newMap.Width, newMap.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(_playersImage);
        using var brush = new SolidBrush( Color.CadetBlue);
        g.FillRectangle(brush, 0,0, _playersImage.Width, _playersImage.Height);
    }
    
    public void RevealAt(PointF unscaledPoint, float brushSize)
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
            g.FillEllipse(_semiRevealBrush, bigger);
            g.FillEllipse(_revealBrush, rect);
        }
        OnRectUpdated?.Invoke(rect);
    }
    
    private void InitializeBrushes(Image newMap)
    {
        _revealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.8f);
        _semiRevealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.2f);
    }

    public void Dispose()
    {
        _playersImage?.Dispose();
    }
}