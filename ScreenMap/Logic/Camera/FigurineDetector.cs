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

    public int MinBlobAreaPx { get; set; } = 800;

    /// <summary>Grayscale diff above this counts as an object. Fixed (not Otsu) so map
    /// texture / screen-photo appearance noise stays below it while an object clears it.</summary>
    public int DiffThreshold { get; set; } = 70;

    /// <summary>When set, <see cref="Detect"/> also crops each detected figurine out of the
    /// aligned frame into <see cref="LastCrops"/>. Off by default so the overlay-only path
    /// stays allocation-free.</summary>
    public bool ProduceCrops { get; set; }

    /// <summary>Isolated, circular-masked crops of the figurines from the most recent
    /// <see cref="Detect"/>, aligned 1:1 with the returned detections (empty unless
    /// <see cref="ProduceCrops"/> is set). Owned by the detector — replaced/disposed on the
    /// next Detect; clone any you need to keep.</summary>
    public Mat[] LastCrops { get; private set; } = Array.Empty<Mat>();

    public DetectStatus Detect(Mat cameraFrame, Bitmap playerView, out FigurineDetection[] detections)
    {
        detections = Array.Empty<FigurineDetection>();
        DisposeCrops();
        if (cameraFrame == null || cameraFrame.Empty() || playerView == null) return DetectStatus.Empty;

        var src = DetectMarkerCenters(cameraFrame);
        if (src == null) return DetectStatus.NoMarkers;

        int dstW = playerView.Width;
        int dstH = playerView.Height;

        // Convert the player-view Bitmap to Mat (BGR) — this is the alignment reference.
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

        // Warp the camera frame so its fiducials land on the REFERENCE's fiducials. Mapping
        // marker-centers -> the reference's own detected marker-centers (not the image
        // corners) aligns the map content even though the filmed screen and the reference
        // are rendered at different resolutions (fiducials sit at the same fraction).
        var refCenters = DetectMarkerCenters(_playerMat);
        if (refCenters == null) return DetectStatus.NoMarkers;

        var srcPts = new[] { src[0], src[1], src[2], src[3] };
        var dstPts = new[] { refCenters[0], refCenters[1], refCenters[2], refCenters[3] };
        using var h = Cv2.GetPerspectiveTransform(srcPts, dstPts);
        EnsureMat(ref _warped, dstH, dstW, MatType.CV_8UC3);
        Cv2.WarpPerspective(cameraFrame, _warped, h, new OpenCvSharp.Size(dstW, dstH));

        EnsureMat(ref _warpedGray, dstH, dstW, MatType.CV_8UC1);
        EnsureMat(ref _playerGray, dstH, dstW, MatType.CV_8UC1);
        Cv2.CvtColor(_warped, _warpedGray, ColorConversionCodes.BGR2GRAY);
        Cv2.CvtColor(_playerMat, _playerGray, ColorConversionCodes.BGR2GRAY);

        // The camera's rendition of the screen is globally brighter/dimmer than the
        // digital reference; without correcting that offset every pixel reads as a diff.
        Cv2.MeanStdDev(_warpedGray, out var warpMean, out _);
        Cv2.MeanStdDev(_playerGray, out var playerMean, out _);
        double brightnessDelta = playerMean.Val0 - warpMean.Val0;
        if (Math.Abs(brightnessDelta) > 1)
            Cv2.Add(_warpedGray, new Scalar(brightnessDelta), _warpedGray);

        EnsureMat(ref _diff, dstH, dstW, MatType.CV_8UC1);
        Cv2.Absdiff(_warpedGray, _playerGray, _diff);

        // Low-pass before diff: smooths out the high-frequency appearance differences
        // between a digital map and a photo of it (bloom, screen texture, sub-pixel
        // registration), while a solid object survives.
        EnsureMat(ref _blur, dstH, dstW, MatType.CV_8UC1);
        Cv2.GaussianBlur(_diff, _blur, new OpenCvSharp.Size(11, 11), 0);

        // Fixed threshold, NOT Otsu: a physical object causes a strong local diff, while
        // map texture / registration residual stays low after brightness normalization.
        // Otsu adapts to the noise floor — it erases a clean diff and explodes on a noisy
        // one — so it is the wrong tool here.
        EnsureMat(ref _thresh, dstH, dstW, MatType.CV_8UC1);
        Cv2.Threshold(_blur, _thresh, DiffThreshold, 255, ThresholdTypes.Binary);

        // Restrict to the play area bounded by the four markers. Outside that quad the warp
        // extrapolates over the screen bezel/border, which always differs wildly — those
        // were the dominant false positives.
        MaskOutsideQuad(_thresh, refCenters.Values);

        // Mask out the fiducial regions (centered on the reference markers, wherever they
        // are) — those always differ between the rendered view and the warped camera frame.
        MaskAround(_thresh, refCenters.Values, 95);

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

        if (ProduceCrops && detections.Length > 0)
            LastCrops = CropDetections(detections);

        return DetectStatus.Ok;
    }

    /// <summary>Cuts each detection out of the aligned frame as a circular-masked crop.
    /// Detections are in the warped/reference coordinate space, so they index <see cref="_warped"/>
    /// directly.</summary>
    // TODO: crops are taken from the deskewed/aligned frame. To show the figurine in true
    // camera perspective, invert the perspective transform (h) and crop the raw cameraFrame.
    private Mat[] CropDetections(FigurineDetection[] detections)
    {
        var bounds = new Rect(0, 0, _warped.Cols, _warped.Rows);
        var crops = new Mat[detections.Length];
        for (int i = 0; i < detections.Length; i++)
        {
            var d = detections[i];
            int r = (int)Math.Ceiling(d.Radius);
            int cx = (int)Math.Round(d.Center.X);
            int cy = (int)Math.Round(d.Center.Y);
            var roi = new Rect(cx - r, cy - r, r * 2, r * 2).Intersect(bounds);
            if (roi.Width <= 0 || roi.Height <= 0) { crops[i] = new Mat(); continue; }

            using var region = new Mat(_warped, roi);
            using var mask = new Mat(roi.Height, roi.Width, MatType.CV_8UC1, Scalar.Black);
            Cv2.Circle(mask, new OpenCvSharp.Point(cx - roi.X, cy - roi.Y), r, Scalar.White, -1);
            // BGRA crop: color from the frame, alpha = the circular mask. Outside the disc
            // stays fully zero (transparent) so the map shows through when drawn on the overlay.
            using var bgra = new Mat();
            Cv2.CvtColor(region, bgra, ColorConversionCodes.BGR2BGRA);
            Cv2.InsertChannel(mask, bgra, 3);
            var masked = new Mat(roi.Height, roi.Width, MatType.CV_8UC4, Scalar.All(0));
            bgra.CopyTo(masked, mask);
            crops[i] = masked;
        }
        return crops;
    }

    private void DisposeCrops()
    {
        foreach (var c in LastCrops) c?.Dispose();
        LastCrops = Array.Empty<Mat>();
    }

    /// <summary>Detects the four corner fiducials and returns id -> centroid, or null if
    /// not all of 0..3 are present.</summary>
    private Dictionary<int, Point2f> DetectMarkerCenters(Mat img)
    {
        CvAruco.DetectMarkers(img, _arucoDict, out var corners, out var ids, _params, out _);
        if (ids == null || ids.Length < 4) return null;
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
            return null;
        return centers;
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

    private static void MaskOutsideQuad(Mat mask, System.Collections.Generic.IEnumerable<Point2f> centers)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var c in centers)
        {
            minX = Math.Min(minX, c.X); maxX = Math.Max(maxX, c.X);
            minY = Math.Min(minY, c.Y); maxY = Math.Max(maxY, c.Y);
        }
        var keep = new Rect((int)minX, (int)minY, (int)(maxX - minX), (int)(maxY - minY))
            .Intersect(new Rect(0, 0, mask.Cols, mask.Rows));
        // Zero everything outside the marker bounding box.
        if (keep.Top > 0) Cv2.Rectangle(mask, new Rect(0, 0, mask.Cols, keep.Top), Scalar.Black, -1);
        if (keep.Bottom < mask.Rows) Cv2.Rectangle(mask, new Rect(0, keep.Bottom, mask.Cols, mask.Rows - keep.Bottom), Scalar.Black, -1);
        if (keep.Left > 0) Cv2.Rectangle(mask, new Rect(0, 0, keep.Left, mask.Rows), Scalar.Black, -1);
        if (keep.Right < mask.Cols) Cv2.Rectangle(mask, new Rect(keep.Right, 0, mask.Cols - keep.Right, mask.Rows), Scalar.Black, -1);
    }

    private static void MaskAround(Mat mask, System.Collections.Generic.IEnumerable<Point2f> centers, int half)
    {
        var black = new Scalar(0);
        foreach (var c in centers)
        {
            int x = (int)c.X - half, y = (int)c.Y - half;
            var rect = new Rect(x, y, half * 2, half * 2).Intersect(new Rect(0, 0, mask.Cols, mask.Rows));
            if (rect.Width > 0 && rect.Height > 0) Cv2.Rectangle(mask, rect, black, -1);
        }
    }

    public void Dispose()
    {
        DisposeCrops();
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
