using System;
using OpenCvSharp;

namespace ScreenMap.Logic.Camera;

public sealed class OpenCvCameraSource : ICameraSource
{
    private readonly VideoCapture _capture;
    private readonly Mat _buffer = new();

    public bool IsOpen => _capture.IsOpened();

    public OpenCvCameraSource(int deviceIndex, int requestedWidth = 1280, int requestedHeight = 720)
    {
        _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
        if (_capture.IsOpened())
        {
            try
            {
                _capture.Set(VideoCaptureProperties.FrameWidth, requestedWidth);
                _capture.Set(VideoCaptureProperties.FrameHeight, requestedHeight);
            }
            catch
            {
                // Resolution request is advisory; some cameras refuse silently.
            }
        }
    }

    public bool TryGrab(out Mat frame)
    {
        frame = null;
        if (!_capture.IsOpened()) return false;
        if (!_capture.Read(_buffer) || _buffer.Empty()) return false;
        frame = _buffer;
        return true;
    }

    public void Dispose()
    {
        _buffer.Dispose();
        _capture.Dispose();
    }

    /// <summary>Probe device indices 0..maxIndex-1 by attempting to open each.</summary>
    public static int[] EnumerateDevices(int maxIndex = 5)
    {
        var found = new System.Collections.Generic.List<int>();
        for (int i = 0; i < maxIndex; i++)
        {
            try
            {
                using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
                if (probe.IsOpened()) found.Add(i);
            }
            catch { }
        }
        return found.ToArray();
    }
}
