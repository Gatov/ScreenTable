using System;
using System.Collections.Generic;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using Point2f = OpenCvSharp.Point2f;

namespace ScreenMap.Logic.Camera;

public enum DetectStatus
{
    Ok,
    NoMarkers,
    Empty
}

public sealed class FigurineDetector : IDisposable
{
    private readonly Dictionary _arucoDict = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
    private readonly DetectorParameters _params = ArucoMarkers.CreateDetectorParameters();

    // Reusable scratch buffers — avoid per-cycle allocations.
    private Mat _warped;
    private Mat _playerMat;
    private Mat _warpedGray;
    private Mat _playerGray;
    private Mat _diff;
    private Mat _blur;
    private Mat _thresh;
    private Mat _morphed;
    private Mat _kernel;

    public int MinBlobAreaPx { get; set; } = 80;

    public DetectStatus Detect(Mat cameraFrame, Bitmap playerView, out FigurineDetection[] detections)
    {
        detections = Array.Empty<FigurineDetection>();
        if (cameraFrame == null || cameraFrame.Empty() || playerView == null) return DetectStatus.Empty;

        CvAruco.DetectMarkers(cameraFrame, _arucoDict, out var corners, out var ids, _params, out _);
        if (ids == null || ids.Length < 4) return DetectStatus.NoMarkers;

        // Map ID -> centroid in camera coords.
        var centers = new Dictionary<int, Point2f>();
        for (int i = 0; i < ids.Length; i++)
        {
            var c = corners[i];
            if (c == null || c.Length < 4) continue;
            float cx = 0, cy = 0;
            for (int k = 0; k < 4; k++) { cx += c[k].X; cy += c[k].Y; }
            centers[ids[i]] = new Point2f(cx / 4f, cy / 4f);
        }
        if (!centers.ContainsKey(0) || !centers.ContainsKey(1) ||
            !centers.ContainsKey(2) || !centers.ContainsKey(3))
            return DetectStatus.NoMarkers;

        int dstW = playerView.Width;
        int dstH = playerView.Height;
        var src = new[] { centers[0], centers[1], centers[2], centers[3] };
        var dst = new[]
        {
            new Point2f(0, 0),
            new Point2f(dstW, 0),
            new Point2f(dstW, dstH),
            new Point2f(0, dstH)
        };

        using var h = Cv2.GetPerspectiveTransform(src, dst);
        EnsureMat(ref _warped, dstH, dstW, MatType.CV_8UC3);
        Cv2.WarpPerspective(cameraFrame, _warped, h, new OpenCvSharp.Size(dstW, dstH));

        // Convert player-view Bitmap to Mat (BGR).
        DisposeIfWrongSize(ref _playerMat, dstH, dstW, MatType.CV_8UC3);
        if (_playerMat == null)
            _playerMat = BitmapConverter.ToMat(playerView);
        else
        {
            using var tmp = BitmapConverter.ToMat(playerView);
            tmp.CopyTo(_playerMat);
        }
        // BitmapConverter produces BGRA for 32bpp argb bitmaps; normalize to BGR.
        if (_playerMat.Channels() == 4)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(_playerMat, bgr, ColorConversionCodes.BGRA2BGR);
            _playerMat.Dispose();
            _playerMat = bgr.Clone();
        }

        EnsureMat(ref _warpedGray, dstH, dstW, MatType.CV_8UC1);
        EnsureMat(ref _playerGray, dstH, dstW, MatType.CV_8UC1);
        Cv2.CvtColor(_warped, _warpedGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(_playerMat, _playerGray, ColorConversionCodes.BGR2GRAY);

        EnsureMat(ref _diff, dstH, dstW, MatType.CV_8UC1);
        Cv2.Absdiff(_warpedGray, _playerGray, _diff);

        EnsureMat(ref _blur, dstH, dstW, MatType.CV_8UC1);
        Cv2.GaussianBlur(_diff, _blur, new OpenCvSharp.Size(5, 5), 0);

        EnsureMat(ref _thresh, dstH, dstW, MatType.CV_8UC1);
        Cv2.Threshold(_blur, _thresh, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // Mask out the 4 corner regions where the ArUco fiducials live —
        // those always differ between rendered view and warped camera frame.
        MaskCorners(_thresh, dstW, dstH, 96);

        if (_kernel == null || _kernel.IsDisposed)
            _kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
        EnsureMat(ref _morphed, dstH, dstW, MatType.CV_8UC1);
        Cv2.MorphologyEx(_thresh, _morphed, MorphTypes.Open, _kernel);

        Cv2.FindContours(_morphed, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var list = new List<FigurineDetection>(contours.Length);
        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < MinBlobAreaPx) continue;
            Cv2.MinEnclosingCircle(contour, out var center, out float radius);
            list.Add(new FigurineDetection(new PointF(center.X, center.Y), radius));
        }
        detections = list.ToArray();
        return DetectStatus.Ok;
    }

    private static void EnsureMat(ref Mat mat, int rows, int cols, MatType type)
    {
        if (mat != null && !mat.IsDisposed && mat.Rows == rows && mat.Cols == cols && mat.Type() == type)
            return;
        mat?.Dispose();
        mat = new Mat(rows, cols, type);
    }

    private static void DisposeIfWrongSize(ref Mat mat, int rows, int cols, MatType type)
    {
        if (mat == null) return;
        if (mat.IsDisposed || mat.Rows != rows || mat.Cols != cols || mat.Type() != type)
        {
            mat.Dispose();
            mat = null;
        }
    }

    private static void MaskCorners(Mat mask, int w, int h, int cornerSize)
    {
        var black = new Scalar(0);
        Cv2.Rectangle(mask, new Rect(0, 0, cornerSize, cornerSize), black, -1);
        Cv2.Rectangle(mask, new Rect(w - cornerSize, 0, cornerSize, cornerSize), black, -1);
        Cv2.Rectangle(mask, new Rect(w - cornerSize, h - cornerSize, cornerSize, cornerSize), black, -1);
        Cv2.Rectangle(mask, new Rect(0, h - cornerSize, cornerSize, cornerSize), black, -1);
    }

    public void Dispose()
    {
        _arucoDict?.Dispose();
        _warped?.Dispose();
        _playerMat?.Dispose();
        _warpedGray?.Dispose();
        _playerGray?.Dispose();
        _diff?.Dispose();
        _blur?.Dispose();
        _thresh?.Dispose();
        _morphed?.Dispose();
        _kernel?.Dispose();
    }
}
