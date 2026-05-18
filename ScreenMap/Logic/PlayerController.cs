using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScreenMap.Controls;
using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

public class PlayerController
{
    private PlayersMapView _mapView;
    PlayersMap _playersMap;

    public Action<MapMessage> OnMessage;

    public byte[] RenderSnapshotPng(Size size)
    {
        using var bitmap = _playersMap?.RenderSnapshot(size);
        if (bitmap == null) return null;
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
    public PlayerController()
    {
    }
    public void SetView(PlayersMapView mapView)
    {
        _mapView = mapView;
        _playersMap = new PlayersMap();
        _playersMap.OnMessage += Publish;
    }
    private void Publish(MapMessage obj)
    {
        OnMessage?.Invoke(obj);
    }
    public void ProcessMessage(MapMessage message)
    {
        switch (message)
        {
            case RevealAtMessage revealAt: _playersMap.RevealAt(revealAt.Location, revealAt.BrushSize, revealAt.Reveal);
                break; 
            case MarkAtMessage markAt: _playersMap.MarkAt(markAt);
                break; 
            case NewImageMessage newImg: using (var ms = new MemoryStream(newImg.Data)) _playersMap.Initialize(ms, newImg.Name);
                _mapView.SetMap(_playersMap);
                break;
            case  GridDataMessage gridData:
                _playersMap.UpdateGrid(gridData);
                break;
            case GridVisibilityMessage gridVis:
                _playersMap.SetGridVisible(gridVis.ShowGrid);
                break;
            case  ZoomInMessage zoomMessage:
                _playersMap.UpdateZoom(zoomMessage);
                break;

            case CenterAtMessage centerAt: _playersMap.CenterAt(centerAt.Location);
                break;
        }
    }

    
}