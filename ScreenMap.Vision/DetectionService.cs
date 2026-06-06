using System;
using System.Drawing;
using OpenCvSharp.Extensions;
using Mat = OpenCvSharp.Mat;

namespace ScreenMap.Vision;

public sealed class DetectionService : IDisposable
{
    private readonly Func<Size, Bitmap> _renderPlayerView;
    private readonly Func<Size, RectangleF> _getViewRect;
    private readonly Func<Size, float> _getPixelsPerCell;
    private readonly Size _playerViewSize;
    private readonly DetectionStore _store;
    private readonly FigurineDetector _detector = new();

    // _gate guards the camera lifecycle fields below. It is held only for short,
    // non-blocking sections — never across RunCycle (which Invokes the UI thread),
    // so teardown on the UI thread can never deadlock against an in-flight tick.
    private readonly object _gate = new();
    private ICameraSource _camera;
    private int _openIndex = -1;          // device index of the currently open camera, or -1.
    private bool _ticking;                // a timer callback currently owns the camera.
    private ICameraSource _disposePending; // camera detached mid-tick; the tick disposes it.
    private System.Threading.Timer _timer;
    private volatile CameraSettings _settings;
    private bool _previewActive;          // a preview window wants frames (UI-thread only).
    private DateTime _lastProcessUtc = DateTime.MinValue;
    private volatile bool _disposed;

    // Latest grabbed frame, published for the preview window. Guarded by _frameLock.
    private readonly object _frameLock = new();
    private Mat _latestFrame;

    // Latest isolated figurine crops (Mats), published for the standalone preview window.
    // Guarded by _cropLock. Produced only while a consumer wants them (preview open, or the
    // figurine overlay is active). The map overlay consumes Bitmap crops via the store.
    private readonly object _cropLock = new();
    private Mat[] _latestCrops = Array.Empty<Mat>();

    // When a preview is open we grab fast (so the feed is smooth) but still only run
    // the (expensive) detection pipeline once per configured interval.
    private const int PreviewTickMs = 66;

    public event Action DetectionsUpdated;
    public DetectionStore Store => _store;

    /// <summary>Renders the current player-view reference image the detector diffs the
    /// warped camera frame against. Used for diagnostics / capturing test fixtures.</summary>
    public Bitmap RenderReferenceView() => _renderPlayerView?.Invoke(_playerViewSize);

    public DetectionService(DetectionStore store, Func<Size, Bitmap> renderPlayerView,
        Func<Size, RectangleF> getViewRect, Size playerViewSize,
        Func<Size, float> getPixelsPerCell = null)
    {
        _store = store;
        _renderPlayerView = renderPlayerView;
        _getViewRect = getViewRect;
        _getPixelsPerCell = getPixelsPerCell;
        _playerViewSize = playerViewSize;
    }

    // Must be called on the UI thread (serializes with SetPreviewActive).
    public void Apply(CameraSettings settings)
    {
        _settings = settings;
        _detector.MinBlobAreaPx = settings.MinBlobAreaPx;
        _detector.MinObjectCells = settings.MinObjectCells;
        _detector.DiffThreshold = settings.DiffThreshold;
        _detector.ExpectedCount = settings.ExpectedFigurines;
        ReconcileCamera();
        DetectionsUpdated?.Invoke();
    }

    /// <summary>
    /// Runs the auto-adjust sweep against the latest camera frame: the user has placed a single
    /// minimal-size (one-cell) token on the map. Finds the sensitivity (and min size) that
    /// isolates it, writes the result into the shared settings, applies it to the live detector,
    /// and persists. Returns the outcome for the caller to display. Call on the UI thread.
    /// </summary>
    public AutoTuneResult AutoTune()
    {
        if (_settings == null)
            return new AutoTuneResult { Success = false, Message = "camera not configured yet" };
        if (!TryGetLatestFrame(out var frame))
            return new AutoTuneResult { Success = false, Message = "no camera frame — open the preview first" };

        using (frame)
        using (var view = RenderReferenceView())
        {
            if (view == null)
                return new AutoTuneResult { Success = false, Message = "no map loaded" };

            float ppc = _getPixelsPerCell?.Invoke(_playerViewSize) ?? 0f;
            var result = new AutoTuner().Tune(frame, view, ppc);
            if (!result.Success) return result;

            _settings.DiffThreshold = result.DiffThreshold;
            if (result.MinObjectCells > 0) _settings.MinObjectCells = result.MinObjectCells;
            else _settings.MinBlobAreaPx = result.MinBlobAreaPx;
            _detector.DiffThreshold = _settings.DiffThreshold;
            _detector.MinObjectCells = _settings.MinObjectCells;
            _detector.MinBlobAreaPx = _settings.MinBlobAreaPx;
            _settings.Save();
            DetectionsUpdated?.Invoke();
            return result;
        }
    }

    /// <summary>
    /// Opens or closes a standalone camera preview. While active, the camera stays open
    /// even if detection is disabled, and frames are grabbed fast for a smooth feed.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetPreviewActive(bool active)
    {
        if (_disposed) return;
        _previewActive = active;
        ReconcileCamera();
        DetectionsUpdated?.Invoke();
    }

    /// <summary>Hands the caller a clone of the most recent camera frame (caller disposes it).</summary>
    public bool TryGetLatestFrame(out Mat frame)
    {
        lock (_frameLock)
        {
            if (_latestFrame == null || _latestFrame.Empty()) { frame = null; return false; }
            frame = _latestFrame.Clone();
            return true;
        }
    }

    /// <summary>Hands the caller clones of the most recent figurine crops (caller disposes
    /// each). Returns false when none were produced this cycle.</summary>
    public bool TryGetLatestCrops(out Mat[] crops)
    {
        lock (_cropLock)
        {
            if (_latestCrops.Length == 0) { crops = Array.Empty<Mat>(); return false; }
            crops = new Mat[_latestCrops.Length];
            for (int i = 0; i < _latestCrops.Length; i++) crops[i] = _latestCrops[i].Clone();
            return true;
        }
    }

    /// <summary>Builds GDI bitmaps (32bpp ARGB, transparent outside the figurine disc) from the
    /// detector's crop Mats, aligned 1:1 with the detections. The store takes ownership.</summary>
    private static Bitmap[] BuildCropBitmaps(Mat[] crops)
    {
        if (crops == null || crops.Length == 0) return Array.Empty<Bitmap>();
        var bmps = new Bitmap[crops.Length];
        for (int i = 0; i < crops.Length; i++)
        {
            if (crops[i] == null || crops[i].Empty()) { bmps[i] = null; continue; }
            try { bmps[i] = BitmapConverter.ToBitmap(crops[i]); }
            catch { bmps[i] = null; }
        }
        return bmps;
    }

    private bool CameraDesired() => (_settings?.Enabled ?? false) || _previewActive;

    // Opens/closes/re-targets the single camera to match the current settings and preview
    // state, then sets the timer cadence. Only crosses the open/closed boundary when
    // necessary, so the common paths never re-open (and never self-contend on the device).
    private void ReconcileCamera()
    {
        bool desired = CameraDesired();
        int wantIndex = _settings?.DeviceIndex ?? 0;

        bool open;
        int curIndex;
        lock (_gate) { open = _camera != null; curIndex = _openIndex; }

        if (open && (!desired || curIndex != wantIndex))
        {
            StopTimer();
            DetachCamera();
            open = false;
        }

        if (!desired)
        {
            StopTimer();
            _store.SetStatus(DetectionStatus.Idle, "off");
            return;
        }

        if (!open && !OpenCamera(wantIndex)) return;

        int intervalMs = _previewActive
            ? PreviewTickMs
            : (int)Math.Max(500, (_settings?.IntervalSeconds ?? 2.0) * 1000);
        if (_timer == null)
            _timer = new System.Threading.Timer(OnTick, null, 200, intervalMs);
        else
            _timer.Change(200, intervalMs);
    }

    private bool OpenCamera(int index)
    {
        ICameraSource cam;
        try
        {
            cam = new OpenCvCameraSource(index);
        }
        catch (Exception ex)
        {
            _store.SetStatus(DetectionStatus.NoDevice, "open failed: " + ex.Message);
            return false;
        }
        if (!cam.IsOpen)
        {
            cam.Dispose(); // release the failed-open handle immediately, don't hold the device.
            _store.SetStatus(DetectionStatus.NoDevice, $"no device {index}");
            return false;
        }
        lock (_gate) { _camera = cam; _openIndex = index; }
        return true;
    }

    private void OnTick(object _)
    {
        ICameraSource cam;
        lock (_gate)
        {
            // Claim the camera. Single-flight (_ticking) and stop checks live under the
            // same lock as teardown, so we never run RunCycle on a disposed camera.
            if (_disposed || _camera == null || _ticking) return;
            _ticking = true;
            cam = _camera;
        }
        try
        {
            RunCycle(cam);
        }
        catch (Exception ex)
        {
            _store.SetStatus(DetectionStatus.Error, ex.Message);
        }
        finally
        {
            ICameraSource toDispose = null;
            bool disposeDetector = false;
            lock (_gate)
            {
                _ticking = false;
                // Teardown ran while we held the camera — we own its disposal now.
                if (_disposePending != null) { toDispose = _disposePending; _disposePending = null; }
                if (_disposed) disposeDetector = true;
            }
            toDispose?.Dispose();
            if (disposeDetector) _detector.Dispose();
            DetectionsUpdated?.Invoke();
        }
    }

    private void RunCycle(ICameraSource camera)
    {
        if (!camera.IsOpen)
        {
            _store.SetStatus(DetectionStatus.NoDevice, "camera closed");
            return;
        }
        if (!camera.TryGrab(out var frame))
        {
            _store.SetStatus(DetectionStatus.Error, "grab failed");
            return;
        }

        PublishFrame(frame);

        // Decouple the grab rate (fast, for a smooth preview) from the detection rate.
        var settings = _settings;
        if (settings == null || !settings.Enabled) return;
        var now = DateTime.UtcNow;
        if ((now - _lastProcessUtc).TotalSeconds < Math.Max(0.5, settings.IntervalSeconds)) return;
        _lastProcessUtc = now;

        using var playerView = _renderPlayerView(_playerViewSize);
        if (playerView == null)
        {
            _store.SetStatus(DetectionStatus.Idle, "no map");
            return;
        }
        var viewRect = _getViewRect(_playerViewSize);
        // Grid scale (px per 2.5 cm cell) for whole-cell radius snapping; 0 disables it.
        _detector.PixelsPerCell = _getPixelsPerCell?.Invoke(_playerViewSize) ?? 0f;
        // Crops are produced when the overlay will draw them (ShowFigurines) or the preview
        // window is showing its own crop montage.
        _detector.ProduceCrops = _previewActive || (settings.ShowFigurines);
        var result = _detector.Detect(frame, playerView, out var dets);
        switch (result)
        {
            case DetectStatus.Ok:
                var translated = TranslateToUnscaled(dets, viewRect, _playerViewSize);
                Bitmap[] cropBmps = Array.Empty<Bitmap>();
                if (_detector.ProduceCrops)
                {
                    PublishCrops(_detector.LastCrops);            // Mats for the preview window
                    cropBmps = BuildCropBitmaps(_detector.LastCrops); // Bitmaps for the map overlay
                }
                // drawn/detected: kept after the score-ranked cap, over raw blobs past the diff threshold.
                _store.Update(translated, cropBmps, DetectionStatus.Ok,
                    $"ok ({translated.Length}/{_detector.LastContourCount}){DiagString()}");
                break;
            case DetectStatus.NoMarkers:
                _store.SetStatus(DetectionStatus.NoMarkers, "no markers");
                break;
            case DetectStatus.Empty:
                _store.SetStatus(DetectionStatus.Error, "empty frame");
                break;
        }
    }

    // TEMP DIAGNOSTIC: " ppc=NN raw=[Rpx=C.Cc,...]" — raw blob radius (px) and its size in cells
    // (raw / ppc) for the last detection, so an over-measured token shows up in the status bar.
    private string DiagString()
    {
        float ppc = _detector.PixelsPerCell;
        var raw = _detector.LastRawRadii;
        if (ppc <= 0 || raw.Length == 0) return ppc > 0 ? $" ppc={ppc:0}" : "";
        var sb = new System.Text.StringBuilder($" ppc={ppc:0} raw=[");
        for (int i = 0; i < raw.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{raw[i]:0}px={raw[i] / ppc:0.0}c");
        }
        sb.Append(']');
        return sb.ToString();
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

    private void PublishFrame(Mat frame)
    {
        lock (_frameLock)
        {
            if (_latestFrame != null &&
                (_latestFrame.Rows != frame.Rows || _latestFrame.Cols != frame.Cols ||
                 _latestFrame.Type() != frame.Type()))
            {
                _latestFrame.Dispose();
                _latestFrame = null;
            }
            if (_latestFrame == null) _latestFrame = frame.Clone();
            else frame.CopyTo(_latestFrame);
        }
    }

    private void ClearLatestFrame()
    {
        lock (_frameLock)
        {
            _latestFrame?.Dispose();
            _latestFrame = null;
        }
    }

    // Owns its own clones — the detector reuses/disposes LastCrops on the next cycle.
    private void PublishCrops(Mat[] crops)
    {
        lock (_cropLock)
        {
            foreach (var c in _latestCrops) c?.Dispose();
            if (crops == null || crops.Length == 0) { _latestCrops = Array.Empty<Mat>(); return; }
            var copy = new Mat[crops.Length];
            for (int i = 0; i < crops.Length; i++) copy[i] = crops[i].Clone();
            _latestCrops = copy;
        }
    }

    private void ClearLatestCrops()
    {
        lock (_cropLock)
        {
            foreach (var c in _latestCrops) c?.Dispose();
            _latestCrops = Array.Empty<Mat>();
        }
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Detaches the current camera. If a tick owns it right now, the camera is handed
    /// to that tick to dispose when it finishes — so we never dispose a VideoCapture
    /// while a native Read is in flight (which can leave the DSHOW device locked).
    /// Never blocks, so it is safe to call from the UI thread.
    /// </summary>
    private void DetachCamera()
    {
        ICameraSource cam = null;
        lock (_gate)
        {
            if (_camera == null) return;
            if (_ticking) _disposePending = _camera;
            else cam = _camera;
            _camera = null;
            _openIndex = -1;
        }
        cam?.Dispose();
        ClearLatestFrame();
        ClearLatestCrops();
    }

    public void Dispose()
    {
        _disposed = true;
        StopTimer();
        ICameraSource cam = null;
        bool disposeDetectorNow = false;
        lock (_gate)
        {
            if (_ticking)
            {
                // The in-flight tick will dispose the camera (and the detector) on exit.
                if (_camera != null) { _disposePending = _camera; _camera = null; _openIndex = -1; }
            }
            else
            {
                cam = _camera;
                _camera = null;
                _openIndex = -1;
                disposeDetectorNow = true;
            }
        }
        cam?.Dispose();
        if (disposeDetectorNow) _detector.Dispose();
        ClearLatestFrame();
        ClearLatestCrops();
    }
}
