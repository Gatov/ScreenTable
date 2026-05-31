using System;
using OpenCvSharp;

namespace ScreenMap.Logic.Camera;

public sealed class OpenCvCameraSource : ICameraSource
{
    private readonly VideoCapture _capture;
    private readonly Mat _buffer = new();

    public bool IsOpen => _capture.IsOpened();

    // Capture at high resolution by default: ArUco fiducials are a small fraction of the
    // frame, so a downsampled 720p feed starves them of pixels and the 4x4 bits can't be
    // decoded. Cameras clamp the request to their max supported mode.
    public OpenCvCameraSource(int deviceIndex, int requestedWidth = 3840, int requestedHeight = 2160)
    {
        _capture = new VideoCapture(deviceIndex, VideoCaptureAPIs.DSHOW);
        if (_capture.IsOpened())
        {
            try
            {
                // Many 4K USB cameras only expose their high-res modes over MJPG; the
                // default (YUY2) is capped to low resolution / frame rate.
                _capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
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
