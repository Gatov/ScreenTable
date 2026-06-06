using System;
using System.Collections.Generic;
using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using Point2f = OpenCvSharp.Point2f;

namespace ScreenMap.Vision;

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
    private Mat _diffBgr;
    private Mat _diff;
    private Mat _blur;
    private Mat _thresh;
    private Mat _morphed;
    private Mat _colorDist; // per-pixel BGR diff-vector magnitude (CV_32FC1), reused each cycle.

    /// <summary>Min blob size in pixels — used only on the no-grid fallback (see
    /// <see cref="PixelsPerCell"/>).</summary>
    public int MinBlobAreaPx { get; set; } = 800;

    /// <summary>Detection-space pixels for one grid cell (one 2.5 cm token), or 0 when the grid
    /// scale is unknown. When set, blob radii are snapped to whole-cell steps and sized in cells
    /// via <see cref="MinObjectCells"/> instead of <see cref="MinBlobAreaPx"/>.</summary>
    public float PixelsPerCell { get; set; }

    /// <summary>Smallest object kept and drawn, measured in grid cells. Only used when
    /// <see cref="PixelsPerCell"/> is set.</summary>
    public double MinObjectCells { get; set; } = 0.3;

    /// <summary>Largest object kept and drawn, measured in grid cells. Only used when
    /// <see cref="PixelsPerCell"/> is set. Rejects massive glare reflections.</summary>
    public double MaxObjectCells { get; set; } = 4.0;

    /// <summary>Minimum fill ratio (contour area / enclosing-circle area). A real token is a solid
    /// disc (~0.5–1.0); diffuse glare and registration smears fill their enclosing circle poorly,
    /// so this rejects them. 0 disables the check.</summary>
    public double MinFillRatio { get; set; } = 0;

    /// <summary>Expected number of figurines on the table. When &gt; 0 the detector keeps only
    /// the <c>ExpectedCount</c> strongest blobs (by <see cref="FigurineDetection.Score"/>); when 0
    /// it auto-guesses via <see cref="SelectCount"/>. Filters fakes/flares without recalibration.</summary>
    public int ExpectedCount { get; set; }

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

    /// <summary>TEMP DIAGNOSTIC: raw (pre-snap) MinEnclosingCircle radius of each kept blob, in
    /// detection-space pixels, aligned 1:1 with the returned detections.</summary>
    public double[] LastRawRadii { get; private set; } = Array.Empty<double>();

    /// <summary>TEMP DIAGNOSTIC: fill ratio (contourArea / enclosing-circle area) of each kept
    /// blob — ~1 for a solid disc, low for a diffuse/ring glare artifact.</summary>
    public double[] LastFillRatios { get; private set; } = Array.Empty<double>();

    /// <summary>Number of raw blobs found right after the diff threshold + morphology, before the
    /// size/fill gates and the score-ranked cap. The "detected" half of drawn/detected.</summary>
    public int LastContourCount { get; private set; }

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
        // marker-centers -> the reference's mathematical marker-centers (not the image
        // corners) aligns the map content even though the filmed screen and the reference
        // are rendered at different resolutions (fiducials sit at the same fraction).
        var refCenters = MarkerRenderer.GetFiducialCenters(new SizeF(dstW, dstH));

        var srcPts = new[] { src[0], src[1], src[2], src[3] };
        var dstPts = new[] { refCenters[0], refCenters[1], refCenters[2], refCenters[3] };
        using var h = Cv2.GetPerspectiveTransform(srcPts, dstPts);
        EnsureMat(ref _warped, dstH, dstW, MatType.CV_8UC3);
        Cv2.WarpPerspective(cameraFrame, _warped, h, new OpenCvSharp.Size(dstW, dstH));

        EnsureMat(ref _diff, dstH, dstW, MatType.CV_8UC1);
        EnsureMat(ref _diffBgr, dstH, dstW, MatType.CV_8UC3);

        // Correct global color cast and brightness differences per channel
        Cv2.MeanStdDev(_warped, out var warpMean, out _);
        Cv2.MeanStdDev(_playerMat, out var playerMean, out _);
        double bDelta = playerMean.Val0 - warpMean.Val0;
        double gDelta = playerMean.Val1 - warpMean.Val1;
        double rDelta = playerMean.Val2 - warpMean.Val2;
        Cv2.Add(_warped, new Scalar(bDelta, gDelta, rDelta), _warped);

        // Use Absdiff instead of Subtract to detect BOTH dark tokens (blocking light)
        // and bright/reflective tokens (reflecting ambient light), relying on morphology to filter glare.
        Cv2.Absdiff(_playerMat, _warped, _diffBgr);

        // Collapse into a single-channel image taking the MAXIMUM difference across B, G, R.
        Mat[] channels = Cv2.Split(_diffBgr);
        Cv2.Max(channels[0], channels[1], _diff);
        Cv2.Max(_diff, channels[2], _diff);
        foreach (var c in channels) c.Dispose();

        // Per-pixel color distance = sqrt(b²+g²+r²) of the BGR diff, reused by the blob scorer.
        EnsureMat(ref _colorDist, dstH, dstW, MatType.CV_32FC1);
        using (var diffF = new Mat())
        {
            _diffBgr.ConvertTo(diffF, MatType.CV_32FC3);
            Mat[] dch = Cv2.Split(diffF);
            try
            {
                _colorDist.SetTo(Scalar.All(0));
                foreach (var c in dch) { using var c2 = c.Mul(c); Cv2.Add(_colorDist, c2, _colorDist); }
                Cv2.Sqrt(_colorDist, _colorDist);
            }
            finally { foreach (var c in dch) c.Dispose(); }
        }

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

        // Apply a Closing operation to bridge gaps and connect fragmented reflections of a single figurine
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(15, 15));
        EnsureMat(ref _morphed, dstH, dstW, MatType.CV_8UC1);
        Cv2.MorphologyEx(_thresh, _morphed, MorphTypes.Close, closeKernel);

        // Apply an Opening operation to remove small noise
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(5, 5));
        Cv2.MorphologyEx(_morphed, _morphed, MorphTypes.Open, openKernel);

        Cv2.FindContours(_morphed, out var contours, out _,
            RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        LastContourCount = contours.Length;

        // Candidate carries everything we need to keep the per-detection arrays 1:1 after
        // the score-ranked cap reorders them.
        var candidates = new List<(FigurineDetection det, double rawRadius, double fill)>(contours.Length);
        foreach (var contour in contours)
        {
            // Apply Convex Hull to convert half-moon/crescent shadows into solid shapes
            var hull = Cv2.ConvexHull(contour);

            Cv2.MinEnclosingCircle(hull, out var center, out float radius);
            float rawRadius = radius;
            double fill = radius > 0 ? Cv2.ContourArea(hull) / (Math.PI * radius * radius) : 0;
            // Reject diffuse / smeared blobs that don't fill their enclosing circle — a solid
            // token does, glare and registration artifacts do not.
            if (MinFillRatio > 0 && fill < MinFillRatio) continue;
            if (PixelsPerCell > 0)
            {
                // Grid scale known. Object size is measured as DIAMETER in cells (a mini occupies
                // ~one grid square == one cell across). Drop anything outside the size band.
                double cells = 2.0 * radius / PixelsPerCell;
                if (cells < MinObjectCells || cells > MaxObjectCells) continue;
            }
            else
            {
                // No grid: legacy pixel-area filter, raw (unsnapped) radius.
                if (Cv2.ContourArea(contour) < MinBlobAreaPx) continue;
            }
            // Score: Distance between the average color of the reference and the average color of the camera frame
            float score = MeanColorDistanceV2(_playerMat, _warped, hull);
            candidates.Add((new FigurineDetection(new PointF(center.X, center.Y), radius, score), rawRadius, fill));
        }

        // Rank by confidence and keep only the expected number (or the auto-guessed count).
        candidates.Sort((a, b) => b.det.Score.CompareTo(a.det.Score));
        var scores = new float[candidates.Count];
        for (int i = 0; i < candidates.Count; i++) scores[i] = candidates[i].det.Score;
        int keep = SelectCount(scores, ExpectedCount);

        var list = new List<FigurineDetection>(keep);
        var rawRadii = new List<double>(keep);
        var fillRatios = new List<double>(keep);
        for (int i = 0; i < keep; i++)
        {
            list.Add(candidates[i].det);
            rawRadii.Add(candidates[i].rawRadius);
            fillRatios.Add(candidates[i].fill);
        }
        detections = list.ToArray();
        LastRawRadii = rawRadii.ToArray();
        LastFillRatios = fillRatios.ToArray();

        if (ProduceCrops && detections.Length > 0)
            LastCrops = CropDetections(detections);

        return DetectStatus.Ok;
    }

    /// <summary>Decides how many of the score-sorted (descending) blobs to keep.
    /// <paramref name="expected"/> &gt; 0 keeps the top N; any other value (0 or negative) auto-guesses.
    /// Auto mode: keep at least <c>Floor</c> and at most <c>Ceiling</c>, cutting at the first
    /// score drop that is sharply larger (&gt; <c>CliffFactor</c>x) than the typical drop across
    /// the top <c>Floor</c>. Pure and OpenCV-free so it is unit-testable.</summary>
    public static int SelectCount(IReadOnlyList<float> scoresDesc, int expected)
    {
        const int floor = 5, ceiling = 20;
        const float cliffFactor = 2.5f;

        int n = scoresDesc.Count;
        if (n == 0) return 0;
        if (expected > 0) return Math.Min(expected, n);
        if (n <= floor) return n;

        // Typical consecutive drop across the assumed-real top group.
        float sumDrop = 0;
        for (int i = 1; i < floor; i++) sumDrop += scoresDesc[i - 1] - scoresDesc[i];
        float refGap = sumDrop / (floor - 1);

        int max = Math.Min(ceiling, n);
        for (int i = floor; i < max; i++)
        {
            float drop = scoresDesc[i - 1] - scoresDesc[i];
            if (drop > cliffFactor * refGap) return i; // cliff: keep the i blobs above it.
        }
        return max; // no cliff found within the window -> ceiling (or all, if fewer).
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

    /// <summary>Mean over the contour's filled interior of the precomputed per-pixel color-distance
    /// magnitude (<paramref name="colorDist"/> is the CV_32FC1 sqrt(b²+g²+r²) of the BGR diff, built
    /// once per cycle in Detect). Higher means the blob differs more strongly — brightness AND hue —
    /// from the map.</summary>
    private static float MeanColorDistance(Mat colorDist, OpenCvSharp.Point[] contour)
    {
        var rect = Cv2.BoundingRect(contour).Intersect(new Rect(0, 0, colorDist.Cols, colorDist.Rows));
        if (rect.Width <= 0 || rect.Height <= 0) return 0f;

        // Mask = the contour's filled interior, in ROI-local coordinates.
        using var mask = new Mat(rect.Height, rect.Width, MatType.CV_8UC1, Scalar.Black);
        var local = new OpenCvSharp.Point[contour.Length];
        for (int i = 0; i < contour.Length; i++)
            local[i] = new OpenCvSharp.Point(contour[i].X - rect.X, contour[i].Y - rect.Y);
        Cv2.FillPoly(mask, new[] { local }, Scalar.White);

        using var roi = new Mat(colorDist, rect);
        return (float)Cv2.Mean(roi, mask).Val0;
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

    /// <summary>
    /// Computes the Euclidean distance between the average color of the reference map and the average color of the camera frame
    /// within the given contour.
    /// </summary>
    private static float MeanColorDistanceV2(Mat refMat, Mat warpedMat, OpenCvSharp.Point[] contour)
    {
        var rect = Cv2.BoundingRect(contour).Intersect(new Rect(0, 0, refMat.Cols, refMat.Rows));
        if (rect.Width <= 0 || rect.Height <= 0) return 0f;

        using var mask = new Mat(rect.Height, rect.Width, MatType.CV_8UC1, Scalar.Black);
        var local = new OpenCvSharp.Point[contour.Length];
        for (int i = 0; i < contour.Length; i++)
            local[i] = new OpenCvSharp.Point(contour[i].X - rect.X, contour[i].Y - rect.Y);
        Cv2.FillPoly(mask, new[] { local }, Scalar.White);

        using var refRoi = new Mat(refMat, rect);
        using var warpedRoi = new Mat(warpedMat, rect);

        Scalar refMean = Cv2.Mean(refRoi, mask);
        Scalar warpedMean = Cv2.Mean(warpedRoi, mask);

        double db = refMean.Val0 - warpedMean.Val0;
        double dg = refMean.Val1 - warpedMean.Val1;
        double dr = refMean.Val2 - warpedMean.Val2;
        float baseScore = (float)Math.Sqrt(db * db + dg * dg + dr * dr);

        // Convert averages to HSV to check for glare
        using var refMat1x1 = new Mat(1, 1, MatType.CV_8UC3);
        refMat1x1.Set<Vec3b>(0, 0, new Vec3b((byte)refMean.Val0, (byte)refMean.Val1, (byte)refMean.Val2));
        using var warpedMat1x1 = new Mat(1, 1, MatType.CV_8UC3);
        warpedMat1x1.Set<Vec3b>(0, 0, new Vec3b((byte)warpedMean.Val0, (byte)warpedMean.Val1, (byte)warpedMean.Val2));

        using var refHsv = new Mat();
        using var warpedHsv = new Mat();
        Cv2.CvtColor(refMat1x1, refHsv, ColorConversionCodes.BGR2HSV);
        Cv2.CvtColor(warpedMat1x1, warpedHsv, ColorConversionCodes.BGR2HSV);

        var refVec = refHsv.Get<Vec3b>(0, 0);
        var warpedVec = warpedHsv.Get<Vec3b>(0, 0);

        float hRef = refVec.Item0; // 0-180
        float vRef = refVec.Item2; // 0-255

        float hWarp = warpedVec.Item0;
        float sWarp = warpedVec.Item1; // 0-255
        float vWarp = warpedVec.Item2;

        float hueDiff = Math.Min(Math.Abs(hWarp - hRef), 180 - Math.Abs(hWarp - hRef));

        // If the blob is noticeably brighter than the map...
        if (vWarp > vRef + 15)
        {
            // Only compare hue if both colors are somewhat saturated (otherwise hue is random noise)
            if (sWarp > 30 && refHsv.Get<Vec3b>(0, 0).Item1 > 30)
            {
                if (hueDiff < 25)
                {
                    // It's brighter and has a similar hue -> likely screen bloom or reflection
                    baseScore -= 1000;
                }
            }
        }

        return baseScore;
    }

    public void Dispose()
    {
        DisposeCrops();
        _arucoDict?.Dispose();
        _warped?.Dispose();
        _playerMat?.Dispose();
        _diffBgr?.Dispose();
        _diff?.Dispose();
        _blur?.Dispose();
        _thresh?.Dispose();
        _morphed?.Dispose();
        _colorDist?.Dispose();
    }
}
