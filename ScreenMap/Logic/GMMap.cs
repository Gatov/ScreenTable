using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using DevExpress.Utils.Drawing;
using Newtonsoft.Json;
using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

/// <summary>
/// Model
/// </summary>
public class GmMap : IDisposable
{
    private Image _originalImage;
    private Image _scaledImage;
    private Bitmap _scaledGmOverlay;
    private MapInfo _mapInfo;
    
    private float _scale;
    private RectangleF _lastSeenClientrect;
    private const float MaxWidth = 4096;
    private const float MaxHeight = 3000;
    public event Action<IMessage> OnMessage;
    public Size Size => _scaledImage?.Size??new Size(1024,1000);

    public MapInfo Info
    {
        get => _mapInfo;
        set => _mapInfo = value;
    }

    public event Action<RectangleF> OnRectUpdated;

    public void LoadMap(string filename)
    {
        _mapInfo = LoadMapInfo(filename);
        var newMap = Image.FromFile(_mapInfo.FileName);
        var data = File.ReadAllBytes(_mapInfo.FileName);
        OnMessage?.Invoke(new NewImageMessage(Path.GetFileNameWithoutExtension(_mapInfo.FileName), data));
        _originalImage = newMap;
        //_scale = Math.Min(1,Math.Min(MaxWidth / newMap.Width, MaxHeight / newMap.Height));
        _scale = 1;
        //_scaledImage = new Bitmap(newMap, (int)Math.Ceiling(newMap.Width*_scale), (int)Math.Ceiling(newMap.Height*_scale));
        _scaledImage = _originalImage;
        UpdateInfo();
        InitializeGmOverlay(newMap);
        if (_mapInfo.History?.Any() == true)
        {
            var old = _mapInfo.History;
            _mapInfo.History = new List<Operation>();
            ReplayHistory(old);
        }
    }

    private void ReplayHistory(List<Operation> mapInfoHistory)
    {
        foreach (var op in mapInfoHistory)
        {
            switch (op.Type)
            {
                case OperationType.RevealAt: RevealAt(new PointF(op.X, op.Y), op.Value, true);
                    break;
                case OperationType.Hide: RevealAt(new PointF(op.X, op.Y), op.Value, false);
                    break;
            }
        }
    }


    private static MapInfo LoadMapInfo(string fileDrop)
    {
        MapInfo mapInfo;
        var jsonFile = GenerateMapInfoFilename(fileDrop);
        if (File.Exists(jsonFile))
        {
            string text = File.ReadAllText(jsonFile);
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

    private static string GenerateMapInfoFilename(string fileDrop)
    {
        return Path.Combine(Path.GetDirectoryName(fileDrop)!, Path.GetFileNameWithoutExtension(fileDrop) + ".json");
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
        _mapInfo.History.Add(new Operation(){X = unscaledPoint.X, Y = unscaledPoint.Y, 
            Type = reveal?OperationType.RevealAt:OperationType.Hide, Value = (int)brushSize});
        OnMessage?.Invoke(new RevealAtMessage(unscaledPoint, brushSize, reveal));
        
        OnRectUpdated?.Invoke(rect);
    }

    public void MarkAt(PointF unscaledPoint, float brushSize, int argbColor)
    {
        var rect = new RectangleF(unscaledPoint.X - brushSize / 2f, unscaledPoint.Y - brushSize / 2f, brushSize, brushSize);
        var markId = _mapInfo.Marks.LastOrDefault()?.Id ?? 0;
        markId++; // next number
        _mapInfo.Marks.Add(new Mark{X = unscaledPoint.X, Y = unscaledPoint.Y,
            ArgbColor = argbColor, Radius = (int)brushSize, Id = markId});
        OnMessage?.Invoke(new MarkAtMessage(unscaledPoint, (int)brushSize, argbColor, markId));
        OnRectUpdated?.Invoke(rect);
    }
    public void RemoveMarkAt(PointF unscaledPos)
    {
        for(int i = _mapInfo.Marks.Count-1; i>=0; i-- )
        {
            var m = _mapInfo.Marks[i];
            if (unscaledPos.Distance(m.AsPoint) < m.Radius/2f)
            {
                _mapInfo.Marks.RemoveAt(i);
                OnMessage?.Invoke(new MarkAtMessage(m.AsPoint, 0, m.ArgbColor, m.Id)); // remove it
                OnRectUpdated?.Invoke(RectangleF.Empty);
                return;
            }
        }
    }

    public void Draw(GraphicsCache g, Rectangle unscaledRect, bool drawFog)
    {
        if (_scaledImage == null) return;
        var scaledRect = unscaledRect.Scale(_scale);
        g.DrawImage(_scaledImage, unscaledRect, scaledRect, GraphicsUnit.Pixel);
        if(drawFog) 
            g.DrawImage(_scaledGmOverlay, unscaledRect, scaledRect, GraphicsUnit.Pixel);
        if (_lastSeenClientrect.IsEmpty == false)
        {
            g.DrawRectangle(Pens.Blue, _lastSeenClientrect.Scale(_scale));
        }

        g.CompositingMode = CompositingMode.SourceOver;
        foreach (var mark in _mapInfo.Marks)
        {
            using var brush = new SolidBrush(Color.FromArgb(mark.ArgbColor));
            g.FillEllipse(brush, new PointF(mark.X, mark.Y).RectByCenter(mark.Radius));
        }
    }

    public void ZoomStep(int ticks)
    {
        OnMessage?.Invoke(new ZoomInMessage() { Ticks = ticks });
    }

    public void UpdateInfo()
    {
        OnMessage?.Invoke(new GridDataMessage(_mapInfo.OffsetX, _mapInfo.OffsetY, _mapInfo.CellSize));       
    }

    public void CenterAt(PointF unscaledPos)
    {
        OnMessage?.Invoke(new CenterAtMessage(){Location = unscaledPos});
    }

    public void SaveMapInfo()
    {
        if(_mapInfo == null ) return;
        var jsonFile = GenerateMapInfoFilename(_mapInfo.FileName);
        var text = JsonConvert.SerializeObject(_mapInfo);
        File.WriteAllText(jsonFile, text);
    }

    public void ProcessMessage(IMessage msg)
    {
        switch (msg)
        {
            case ClientRectangleMessage clRect: _lastSeenClientrect = clRect.Rectangle; OnRectUpdated?.Invoke(RectangleF.Empty);
                break; 
        }
    }


}