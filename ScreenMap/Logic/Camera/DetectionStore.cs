using System;
using System.Drawing;

namespace ScreenMap.Logic.Camera;

public enum DetectionStatus
{
    Idle,
    Ok,
    NoMarkers,
    NoDevice,
    Error
}

public sealed class DetectionStore : IDisposable
{
    // A detection result and its aligned figurine crops, swapped as one immutable unit so a
    // reader never pairs detections from one cycle with crops from another.
    private sealed class Frame
    {
        public FigurineDetection[] Dets = Array.Empty<FigurineDetection>();
        public Bitmap[] Crops = Array.Empty<Bitmap>();   // index-aligned with Dets; entry may be null
    }

    private static readonly Frame Empty = new();

    // _current is what readers (UI-thread paints) see. _previous is the one-generation
    // reserve kept alive so a paint that captured the just-replaced frame can finish before
    // its bitmaps are disposed. Updates land at the detection interval (>=0.5s) while a paint
    // completes in well under a frame, so the reserve is always ample.
    private volatile Frame _current = Empty;
    private Frame _previous;
    private readonly object _swapLock = new();

    private volatile string _statusText = "idle";
    private DetectionStatus _status = DetectionStatus.Idle;

    public FigurineDetection[] Snapshot() => _current.Dets;

    /// <summary>Atomically reads the current detections together with their aligned crop
    /// bitmaps (an entry may be null). Both come from the same cycle.</summary>
    public (FigurineDetection[] Dets, Bitmap[] Crops) SnapshotFrame()
    {
        var f = _current;
        return (f.Dets, f.Crops);
    }

    public DetectionStatus Status => _status;
    public string StatusText => _statusText;

    /// <summary>Publishes a new detection result. The store takes ownership of <paramref name="crops"/>
    /// and disposes them once they age out of the reserve (or on <see cref="Dispose"/>).</summary>
    public void Update(FigurineDetection[] detections, Bitmap[] crops, DetectionStatus status, string text)
    {
        var frame = new Frame
        {
            Dets = detections ?? Array.Empty<FigurineDetection>(),
            Crops = crops ?? Array.Empty<Bitmap>(),
        };
        Frame aged;
        lock (_swapLock)
        {
            aged = _previous;     // two generations old — no live paint can still hold it
            _previous = _current; // keep the just-replaced frame alive one more cycle
            _current = frame;
        }
        _status = status;
        _statusText = text ?? string.Empty;
        DisposeFrame(aged);
    }

    public void SetStatus(DetectionStatus status, string text)
    {
        _status = status;
        _statusText = text ?? string.Empty;
    }

    private static void DisposeFrame(Frame f)
    {
        if (f == null) return;
        foreach (var c in f.Crops) c?.Dispose();
    }

    public void Dispose()
    {
        lock (_swapLock)
        {
            DisposeFrame(_previous);
            DisposeFrame(_current);
            _previous = null;
            _current = Empty;
        }
    }
}
