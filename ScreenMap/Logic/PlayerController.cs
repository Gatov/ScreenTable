using System;
using System.IO;
using ScreenMap.Controls;
using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

public class PlayerController
{
    private PlayersMapView _mapView;
    PlayersMap _playersMap;

    public Action<IMessage> OnMessage;
    public PlayerController()
    {
    }
    public void SetView(PlayersMapView mapView)
    {
        _mapView = mapView;
        _playersMap = new PlayersMap();
        _playersMap.OnMessage += Publish;
    }
    private void Publish(IMessage obj)
    {
        OnMessage?.Invoke(obj);
    }
    public void ProcessMessage(IMessage message)
    {
        switch (message)
        {
            case RevealAtMessage revealAt: _playersMap.RevealAt(revealAt.Location, revealAt.BrushSize, revealAt.Reveal);
                break; 
            case NewImageMessage newImg: using (var ms = new MemoryStream(newImg.Data)) _playersMap.Initialize(ms);
                _mapView.SetMap(_playersMap);
                break;
            case  GridDataMessage gridData:
                _playersMap.UpdateGrid(gridData);
                break;

            case CenterAtMessage centerAt: _playersMap.CenterAt(centerAt.Location);
                break;
        }
    }

    
}