using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DevExpress.Utils.Drawing;
using Newtonsoft.Json;
using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

/// <summary>
/// Model
/// </summary>
public class GMMap : IDisposable
{
    private readonly Image _originalImage;
    private readonly Bitmap _scaledImage;
    private Bitmap _scaledGmOverlay;
    private MapInfo _mapInfo;
    
    private float _scale;
    public static float MaxWidth = 1920;
    public static float MaxHeight = 1080;
    public event Action<IMessage> OnMessage;

    public Image OriginalImage => _originalImage;

    public MapInfo Info
    {
        get => _mapInfo;
        set => _mapInfo = value;
    }

    public event Action<RectangleF> OnRectUpdated;
    public GMMap(string filename)
    {
        _mapInfo = LoadMapInfo(filename);
        var newMap = Image.FromFile(_mapInfo.FileName);
        var data = File.ReadAllBytes(_mapInfo.FileName);
        OnMessage?.Invoke(new NewImageMessage(Path.GetFileNameWithoutExtension(_mapInfo.FileName), data));
        _originalImage = newMap;
        _scale = Math.Min(1,Math.Min(MaxWidth / newMap.Width, MaxHeight / newMap.Height));
        _scaledImage = new Bitmap(newMap, (int)Math.Ceiling(newMap.Width*_scale), (int)Math.Ceiling(newMap.Height*_scale));
        UpdateInfo();
        
        InitializeGmOverlay(newMap);
        

    }
    
    private static MapInfo LoadMapInfo(string fileDrop)
    {
        MapInfo mapInfo;
        if (Path.GetExtension(fileDrop) == ".json")
        {
            string text = File.ReadAllText(fileDrop);
            mapInfo = JsonConvert.DeserializeObject<MapInfo>(text);
        }
        else
        {
            mapInfo = new MapInfo() { FileName = fileDrop };
        }

        if (File.Exists(mapInfo.FileName) == false) // files were moved, combine new path with filename
            mapInfo.FileName = Path.Combine(Path.GetDirectoryName(fileDrop)!, Path.GetFileName(mapInfo.FileName)!);
        return mapInfo;
    }
    private void InitializeGmOverlay(Image newMap)
    {
        _scaledGmOverlay = new Bitmap( (int)Math.Ceiling(newMap.Width*_scale), (int)Math.Ceiling(newMap.Height*_scale),PixelFormat.Format32bppArgb);

        using Graphics g = Graphics.FromImage(_scaledGmOverlay);
        using var brush = new SolidBrush(Color.FromArgb(200, Color.Gray));
        g.FillRectangle(brush, 0, 0, _scaledGmOverlay.Width, _scaledGmOverlay.Height);
    }


   

    public void Dispose()
    {
        _originalImage?.Dispose();
        _scaledImage?.Dispose();
        _scaledGmOverlay?.Dispose();
    }

    public void RevealAt(PointF unscaledPoint, float brushSize, bool reveal)
    {
        var rect = new RectangleF(unscaledPoint.X - brushSize / 2f, unscaledPoint.Y - brushSize / 2f, brushSize, brushSize);
        using (Graphics g = Graphics.FromImage(_scaledGmOverlay))
        {
            g.ScaleTransform(_scale, _scale);
            g.CompositingMode = CompositingMode.SourceCopy;
            //g.ScaleTransform(1/_currentGmZoom, 1/_currentGmZoom);
            // create a radial brush with alpha increasing towards the center
            var alpha = reveal ? 0 : 200;
            using (var brush = new SolidBrush(Color.FromArgb(alpha, Color.Gray)))
            {
                //set anti-aliasing
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, rect);
            }
        }
        OnMessage?.Invoke(new RevealAtMessage(unscaledPoint, brushSize, reveal));
        
        OnRectUpdated?.Invoke(rect);
    }

    

    public void Draw(GraphicsCache g, Rectangle unscaledRect, bool drawFog)
    {
        var scaledRect = RectangleF.FromLTRB(unscaledRect.Left * _scale, unscaledRect.Top * _scale,
            unscaledRect.Right * _scale, unscaledRect.Bottom * _scale);
        g.DrawImage(_scaledImage, unscaledRect, scaledRect, GraphicsUnit.Pixel);
        if(drawFog) 
            g.DrawImage(_scaledGmOverlay, unscaledRect, scaledRect, GraphicsUnit.Pixel);
    }

    public void ZoomStep(int ticks)
    {
        // TODO:
        //throw new NotImplementedException();
    }

    public void UpdateInfo()
    {
        OnMessage?.Invoke(new GridDataMessage(_mapInfo.OffsetX, _mapInfo.OffsetY, _mapInfo.CellSize));       
    }
}