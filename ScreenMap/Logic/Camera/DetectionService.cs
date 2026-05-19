using System;
using System.Drawing;
using System.Threading;

namespace ScreenMap.Logic.Camera;

public sealed class DetectionService : IDisposable
{
    private readonly Func<Size, Bitmap> _renderPlayerView;
    private readonly Func<Size, RectangleF> _getViewRect;
    private readonly Size _playerViewSize;
    private readonly DetectionStore _store;
    private readonly FigurineDetector _detector = new();

    private ICameraSource _camera;
    private System.Threading.Timer _timer;
    private CameraSettings _settings;
    private int _running; // 0/1 — single-flight guard for the timer callback.
    private volatile bool _disposed;

    public event Action DetectionsUpdated;
    public DetectionStore Store => _store;

    public DetectionService(DetectionStore store, Func<Size, Bitmap> renderPlayerView,
        Func<Size, RectangleF> getViewRect, Size playerViewSize)
    {
        _store = store;
        _renderPlayerView = renderPlayerView;
        _getViewRect = getViewRect;
        _playerViewSize = playerViewSize;
    }

    public void Apply(CameraSettings settings)
    {
        _settings = settings;
        _detector.MinBlobAreaPx = settings.MinBlobAreaPx;
        StopTimer();
        DisposeCamera();
        if (!settings.Enabled)
        {
            _store.SetStatus(DetectionStatus.Idle, "off");
            DetectionsUpdated?.Invoke();
            return;
        }
        try
        {
            _camera = new OpenCvCameraSource(settings.DeviceIndex);
            if (!_camera.IsOpen)
            {
                _store.SetStatus(DetectionStatus.NoDevice, $"no device {settings.DeviceIndex}");
                DetectionsUpdated?.Invoke();
                return;
            }
        }
        catch (Exception ex)
        {
            _store.SetStatus(DetectionStatus.NoDevice, "open failed: " + ex.Message);
            DetectionsUpdated?.Invoke();
            return;
        }

        var intervalMs = (int)Math.Max(500, settings.IntervalSeconds * 1000);
        _timer = new System.Threading.Timer(OnTick, null, 500, intervalMs);
    }

    private void OnTick(object _)
    {
        if (_disposed) return;
        if (Interlocked.Exchange(ref _running, 1) == 1) return;
        try
        {
            RunCycle();
        }
        catch (Exception ex)
        {
            _store.SetStatus(DetectionStatus.Error, ex.Message);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
            DetectionsUpdated?.Invoke();
        }
    }

    private void RunCycle()
    {
        if (_camera == null || !_camera.IsOpen)
        {
            _store.SetStatus(DetectionStatus.NoDevice, "camera closed");
            return;
        }
        if (!_camera.TryGrab(out var frame))
        {
            _store.SetStatus(DetectionStatus.Error, "grab failed");
            return;
        }
        using var playerView = _renderPlayerView(_playerViewSize);
        if (playerView == null)
        {
            _store.SetStatus(DetectionStatus.Idle, "no map");
            return;
        }
        var viewRect = _getViewRect(_playerViewSize);
        var result = _detector.Detect(frame, playerView, out var dets);
        switch (result)
        {
            case DetectStatus.Ok:
                var translated = TranslateToUnscaled(dets, viewRect, _playerViewSize);
                _store.Update(translated, DetectionStatus.Ok, $"ok ({translated.Length})");
                break;
            case DetectStatus.NoMarkers:
                _store.SetStatus(DetectionStatus.NoMarkers, "no markers");
                break;
            case DetectStatus.Empty:
                _store.SetStatus(DetectionStatus.Error, "empty frame");
                break;
        }
    }

    private static FigurineDetection[] TranslateToUnscaled(FigurineDetection[] dets, RectangleF view, Size size)
    {
        if (view.IsEmpty || size.Width == 0 || size.Height == 0) return dets;
        float sx = view.Width / size.Width;
        float sy = view.Height / size.Height;
        float rs = (sx + sy) / 2f;
        var result = new FigurineDetection[dets.Length];
        for (int i = 0; i < dets.Length; i++)
        {
            var d = dets[i];
            result[i] = new FigurineDetection(
                new PointF(view.X + d.Center.X * sx, view.Y + d.Center.Y * sy),
                d.Radius * rs);
        }
        return result;
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void DisposeCamera()
    {
        _camera?.Dispose();
        _camera = null;
    }

    public void Dispose()
    {
        _disposed = true;
        StopTimer();
        DisposeCamera();
        _detector.Dispose();
    }
}
