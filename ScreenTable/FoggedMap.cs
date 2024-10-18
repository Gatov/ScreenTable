using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Serialization;

namespace ScreenTable;

/// <summary>
/// Model
/// </summary>
public class FoggedMap : IDisposable
{
    private Image _originalImage;
    private Bitmap _playersImage;
    private Bitmap _scaledImage;
    private Bitmap _scaledGmOverlay;
    
    private TextureBrush _revealBrush;
    private TextureBrush _semiRevealBrush;

    private float _scale;
    public static float MaxWidth = 1920;
    public static float MaxHeight = 1080;

    public event Action<RectangleF> OnRectUpdated;
    public FoggedMap(string filename)
    {
        var newMap = Image.FromFile(filename);
        _originalImage = newMap;
        _scale = Math.Min(1,Math.Min(MaxWidth / newMap.Width, MaxHeight / newMap.Height));
        _scaledImage = new Bitmap(newMap, (int)Math.Ceiling(newMap.Width*_scale), (int)Math.Ceiling(newMap.Height*_scale));
        InitializeGmOverlay(newMap);
        InitializePlayerImage(newMap);
        InitializeBrushes(newMap);
    }

    private void InitializeBrushes(Image newMap)
    {
        _revealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.8f);
        _semiRevealBrush = FogUtil.CreateSemitransparentBrushFromImage(newMap, 0.2f);
    }

    private void InitializeGmOverlay(Image newMap)
    {
        _scaledGmOverlay = new Bitmap( (int)Math.Ceiling(newMap.Width*_scale), (int)Math.Ceiling(newMap.Height*_scale),PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(_scaledGmOverlay);
        using var brush = new SolidBrush(Color.FromArgb(200, Color.Gray));
        g.FillRectangle(brush, 0, 0, _scaledGmOverlay.Width, _scaledGmOverlay.Height);
    }

    private void InitializePlayerImage(Image newMap)
    {
        _playersImage = new Bitmap(newMap.Width, newMap.Height, PixelFormat.Format32bppArgb);
        using Graphics g = Graphics.FromImage(_playersImage);
        using var brush = new SolidBrush( Color.CadetBlue);
        g.FillRectangle(brush, 0,0, _playersImage.Width, _playersImage.Height);
    }

    public Image OriginalImage => _originalImage;

    public Image PlayersImage => _playersImage;

    public void Dispose()
    {
        _semiRevealBrush?.Dispose();
        _revealBrush?.Dispose();
        _playersImage?.Dispose();
        _originalImage?.Dispose();
        _scaledImage?.Dispose();
        _scaledGmOverlay?.Dispose();
    }

    public void RevealAt(PointF unscaledPoint, float brushSize)
    {
        var rect = new RectangleF(unscaledPoint.X - brushSize / 2f, unscaledPoint.Y - brushSize / 2f, brushSize, brushSize);
        using (Graphics g = Graphics.FromImage(_scaledGmOverlay))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.ScaleTransform(_scale, _scale);
            //g.ScaleTransform(1/_currentGmZoom, 1/_currentGmZoom);
            // create a radial brush with alpha increasing towards the center

            using (var brush = new SolidBrush(Color.FromArgb(0, Color.Aqua)))
            {
                //set anti-aliasing
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, rect);
            }
        }
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

    

    public void Draw(Graphics g, Rectangle unscaledRect, bool drawFog)
    {
        var scaledRect = RectangleF.FromLTRB(unscaledRect.Left * _scale, unscaledRect.Top * _scale,
            unscaledRect.Right * _scale, unscaledRect.Bottom * _scale);
        g.DrawImage(_scaledImage, unscaledRect, scaledRect, GraphicsUnit.Pixel);
        if(drawFog) 
            g.DrawImage(_scaledGmOverlay, unscaledRect, scaledRect, GraphicsUnit.Pixel);
    }
}