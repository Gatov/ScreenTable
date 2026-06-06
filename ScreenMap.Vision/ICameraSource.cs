using System;
using OpenCvSharp;

namespace ScreenMap.Vision;

public interface ICameraSource : IDisposable
{
    bool IsOpen { get; }

    /// <summary>
    /// Grabs the latest frame from the camera. Returns false if no frame is available
    /// or the device is not open. The returned Mat is owned by the source and reused
    /// across calls; copy it if you need to keep it past the next TryGrab call.
    /// </summary>
    bool TryGrab(out Mat frame);
}
