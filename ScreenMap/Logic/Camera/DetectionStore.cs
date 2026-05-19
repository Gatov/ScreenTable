using System;

namespace ScreenMap.Logic.Camera;

public enum DetectionStatus
{
    Idle,
    Ok,
    NoMarkers,
    NoDevice,
    Error
}

public sealed class DetectionStore
{
    private volatile FigurineDetection[] _detections = Array.Empty<FigurineDetection>();
    private volatile string _statusText = "idle";
    private DetectionStatus _status = DetectionStatus.Idle;

    public FigurineDetection[] Snapshot() => _detections;
    public DetectionStatus Status => _status;
    public string StatusText => _statusText;

    public void Update(FigurineDetection[] detections, DetectionStatus status, string text)
    {
        _detections = detections ?? Array.Empty<FigurineDetection>();
        _status = status;
        _statusText = text ?? string.Empty;
    }

    public void SetStatus(DetectionStatus status, string text)
    {
        _status = status;
        _statusText = text ?? string.Empty;
    }
}
