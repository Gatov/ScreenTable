using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using ScreenMap.Controls;
using ScreenMap.Logic.Camera;
using ScreenMap.Logic.Messages;

namespace ScreenMap.Logic;

public class PlayerController
{
    private PlayersMapView _mapView;
    PlayersMap _playersMap;

    private readonly object _snapshotLock = new();
    private byte[] _cachedSnapshot;
    private Size _cachedSize;
    private volatile bool _snapshotDirty = true;

    public Action<MapMessage> OnMessage;

    private DetectionStore _detectionStore;
    private Func<bool> _showDetections;
    public void SetDetectionOverlay(DetectionStore store, Func<bool> showFlag)
    {
        _detectionStore = store;
        _showDetections = showFlag;
        _playersMap?.SetDetectionOverlay(store, showFlag);
    }

    public void InvalidateSnapshot() => _snapshotDirty = true;

    // Returns a cached PNG if the map state is unchanged since the last render.
    // Safe to call from any thread; lets the web server skip the UI-thread round-trip.
    public byte[] TryGetCachedSnapshotPng(Size size)
    {
        lock (_snapshotLock)
        {
            if (!_snapshotDirty && _cachedSnapshot != null && _cachedSize == size)
                return _cachedSnapshot;
        }
        return null;
    }

    /// <summary>Render a snapshot bitmap for off-screen consumers (e.g. camera detection).
    /// Caller owns and must dispose the returned bitmap. Returns null if no map is loaded.</summary>
    public Bitmap RenderSnapshotBitmap(Size size)
    {
        return _playersMap?.RenderSnapshot(size);
    }

    public RectangleF GetSnapshotViewRect(Size size)
    {
        return _playersMap?.GetSnapshotViewRect(size) ?? RectangleF.Empty;
    }

    public PlayersMap PlayersMap => _playersMap;

    public byte[] RenderSnapshotPng(Size size)
    {
        using var bitmap = _playersMap?.RenderSnapshot(size);
        if (bitmap == null) return null;
        using var ms = new MemoryStream();
        bitmap.Save(ms, ImageFormat.Png);
        var bytes = ms.ToArray();
        lock (_snapshotLock)
        {
            _cachedSnapshot = bytes;
            _cachedSize = size;
            _snapshotDirty = false;
        }
        return bytes;
    }
    public PlayerController()
    {
    }
    public void SetView(PlayersMapView mapView)
    {
        if (_playersMap != null)
        {
            _playersMap.OnMessage -= Publish;
            _playersMap.OnRectUpdated -= OnMapDirty;
            _playersMap.Dispose();
        }
        _mapView = mapView;
        _playersMap = new PlayersMap();
        _playersMap.OnMessage += Publish;
        _playersMap.OnRectUpdated += OnMapDirty;
        if (_detectionStore != null)
            _playersMap.SetDetectionOverlay(_detectionStore, _showDetections);
    }

    private void OnMapDirty(RectangleF _) => _snapshotDirty = true;
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