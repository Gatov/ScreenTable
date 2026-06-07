using System;
using System.Collections.Generic;
using System.Drawing;
using OpenCvSharp;

namespace ScreenMap.Vision;

/// <summary>Outcome of an auto-adjust sweep: the sensitivity (and size) the detector should use
/// to isolate the single minimal token the user placed, plus a human-readable summary.</summary>
public sealed class AutoTuneResult
{
    public bool Success;
    public int DiffThreshold;
    /// <summary>Min object size in grid cells (0 when the grid scale is unknown).</summary>
    public double MinObjectCells;
    /// <summary>Min object size in pixels — set only on the no-grid fallback.</summary>
    public int MinBlobAreaPx;
    /// <summary>Number of blobs at the chosen threshold.</summary>
    public int BlobCount;
    /// <summary>Diameter (in cells) of the token the pick was based on.</summary>
    public double TokenDiameterCells;
    public double LensDistortionK1;
    public double LensDistortionCx;
    public double LensDistortionCy;
    public string Message = "";
}

/// <summary>
/// Finds the detector parameters that isolate a single, minimal-size token. The user places one
/// 2.5 cm (one-cell) token on the screen; <see cref="Tune"/> sweeps the sensitivity threshold over
/// the captured frame and <see cref="SelectThreshold"/> picks the value that yields exactly that
/// one token-sized blob. Grid-aware: with a known pixels-per-cell the token is expected at one
/// cell radius; without a grid it falls back to a pixel-area heuristic.
/// </summary>
public sealed class AutoTuner
{
    public const int MinThreshold = 20;
    public const int MaxThreshold = 150;
    public const int ThresholdStep = 5;

    /// <summary>Distortion sweep: tries each lens k1 over a single frame and returns the one that
    /// minimizes the total raw-blob confidence energy (a straightened map produces the fewest
    /// spurious diffs). Does NOT pick a sensitivity — that is <see cref="TuneSensitivity"/>. Uses
    /// its own detector instance so it never races the live detector. Hard-aborts when the markers
    /// can't be found at any threshold/k1.</summary>
    public AutoTuneResult TuneDistortion(Mat cameraFrame, Bitmap referenceView, float pixelsPerCell)
    {
        if (cameraFrame == null || cameraFrame.Empty() || referenceView == null)
            return new AutoTuneResult { Success = false, Message = "no camera frame or reference view" };

        double cx = cameraFrame.Width / 2.0;
        double cy = cameraFrame.Height / 2.0;

        double bestK1 = 0;
        double minTotalConfidence = double.MaxValue;
        bool anyK1Ok = false;

        // The tuner clamps its final minArea recommendation to 200px at the lowest
        using var detector = new FigurineDetector { MinBlobAreaPx = 200 };
        using Mat undistortedFrame = new Mat();

        for (double k1 = 0.0; k1 <= 0.201; k1 += 0.025)
        {
            if (Math.Abs(k1) > 0.001)
                FigurineDetector.Undistort(cameraFrame, undistortedFrame, k1);
            else
                cameraFrame.CopyTo(undistortedFrame);

            RunThresholdSweep(detector, undistortedFrame, referenceView, pixelsPerCell,
                out double totalConfidence, out bool anyOk);

            if (anyOk && totalConfidence < minTotalConfidence)
            {
                minTotalConfidence = totalConfidence;
                bestK1 = k1;
                anyK1Ok = true;
            }
        }

        if (!anyK1Ok)
            return new AutoTuneResult
            {
                Success = false,
                Message = "markers not found — aim the camera so all four corners are visible"
            };

        return new AutoTuneResult
        {
            Success = true,
            LensDistortionK1 = bestK1,
            LensDistortionCx = cx,
            LensDistortionCy = cy,
            Message = $"lens distortion k1 = {bestK1:F3}"
        };
    }

    /// <summary>Sensitivity sweep: undistorts the frame ONCE with the supplied <paramref name="k1"/>
    /// (the stored / chosen lens coefficient), sweeps the diff threshold, and picks the sensitivity
    /// + min size that isolate the single placed token via <see cref="SelectThreshold"/>. Hard-aborts
    /// when the markers can't be found at any threshold.</summary>
    public AutoTuneResult TuneSensitivity(Mat cameraFrame, Bitmap referenceView,
        float pixelsPerCell, double k1)
    {
        if (cameraFrame == null || cameraFrame.Empty() || referenceView == null)
            return new AutoTuneResult { Success = false, Message = "no camera frame or reference view" };

        using var detector = new FigurineDetector { MinBlobAreaPx = 200 };
        using Mat undistortedFrame = new Mat();
        if (Math.Abs(k1) > 0.001)
            FigurineDetector.Undistort(cameraFrame, undistortedFrame, k1);
        else
            cameraFrame.CopyTo(undistortedFrame);

        var sweep = RunThresholdSweep(detector, undistortedFrame, referenceView, pixelsPerCell,
            out _, out bool anyOk);

        if (!anyOk || sweep.Count == 0)
            return new AutoTuneResult
            {
                Success = false,
                Message = "markers not found — aim the camera so all four corners are visible"
            };

        var result = SelectThreshold(sweep, pixelsPerCell);
        result.LensDistortionK1 = k1;
        return result;
    }

    /// <summary>Convenience: run the distortion sweep, then the sensitivity sweep with the chosen
    /// k1, returning a single result carrying both. Kept for callers/tests that want the combined
    /// behavior; the UI drives <see cref="TuneDistortion"/> and <see cref="TuneSensitivity"/>
    /// independently.</summary>
    public AutoTuneResult Tune(Mat cameraFrame, Bitmap referenceView, float pixelsPerCell)
    {
        var d = TuneDistortion(cameraFrame, referenceView, pixelsPerCell);
        if (!d.Success) return d;
        var s = TuneSensitivity(cameraFrame, referenceView, pixelsPerCell, d.LensDistortionK1);
        if (!s.Success) return s;
        s.LensDistortionK1 = d.LensDistortionK1;
        s.LensDistortionCx = d.LensDistortionCx;
        s.LensDistortionCy = d.LensDistortionCy;
        return s;
    }

    /// <summary>Runs the detector across the threshold range on one (already-undistorted) frame.
    /// Returns the per-threshold blob radii, the summed raw confidence energy, and whether the
    /// markers were found at any threshold. Token-implausible frames (a blob &gt; 4.5 cells across)
    /// contribute an empty radii list at that threshold.</summary>
    private static List<(int threshold, double[] radiiPx)> RunThresholdSweep(
        FigurineDetector detector, Mat frame, Bitmap referenceView, float pixelsPerCell,
        out double totalConfidence, out bool anyOk)
    {
        var sweep = new List<(int threshold, double[] radiiPx)>();
        totalConfidence = 0;
        anyOk = false;

        for (int t = MinThreshold; t <= MaxThreshold; t += ThresholdStep)
        {
            detector.DiffThreshold = t;
            var status = detector.Detect(frame, referenceView, out var dets);
            if (status != DetectStatus.Ok) continue;
            anyOk = true;

            var validRadii = new List<double>();
            for (int i = 0; i < dets.Length; i++)
            {
                if (pixelsPerCell > 0)
                {
                    double cells = 2.0 * dets[i].Radius / pixelsPerCell;
                    if (cells > 4.5)
                    {
                        validRadii.Clear();
                        break;
                    }
                }
                validRadii.Add(dets[i].Radius);
            }
            sweep.Add((t, validRadii.ToArray()));
            totalConfidence += detector.LastTotalConfidence;
        }
        return sweep;
    }

    /// <summary>Pure pick step: from the blob radii each threshold produced, choose the sensitivity
    /// that isolates the single token. Best-effort — always returns a recommendation unless there is
    /// no data / no blobs at all.</summary>
    public static AutoTuneResult SelectThreshold(
        IReadOnlyList<(int threshold, double[] radiiPx)> sweep, float pixelsPerCell)
    {
        if (sweep == null || sweep.Count == 0)
            return new AutoTuneResult { Success = false, Message = "no sweep data" };

        return pixelsPerCell > 0 ? SelectGrid(sweep, pixelsPerCell) : SelectArea(sweep);
    }

    private static AutoTuneResult SelectGrid(
        IReadOnlyList<(int threshold, double[] radiiPx)> sweep, float ppc)
    {
        // Object size is measured as DIAMETER in cells. A blob counts as a token when it spans at
        // least 0.3 cells across; sub-cell blobs are noise.
        double DiaCells(double r) => 2.0 * r / ppc;
        bool TokenSized(double r) => DiaCells(r) >= 0.3;

        // A threshold qualifies when it yields exactly one blob and that blob is at least one cell.
        bool Qualifies(int i) => sweep[i].radiiPx.Length == 1 && TokenSized(sweep[i].radiiPx[0]);

        var (start, len) = LongestRun(sweep.Count, Qualifies);
        if (len > 0)
        {
            int idx = start + len / 2;
            int center = sweep[idx].threshold;
            double dia = Math.Floor(DiaCells(sweep[idx].radiiPx[0]) * 100) / 100.0;
            double safeMinDia = Math.Max(0.3, dia * 0.6);
            return new AutoTuneResult
            {
                Success = true,
                DiffThreshold = center,
                MinObjectCells = safeMinDia,
                TokenDiameterCells = dia,
                BlobCount = 1,
                Message = $"sensitivity {center}, min size {safeMinDia:F2} cells"
            };
        }

        // Best-effort among thresholds that actually contain a token (>= one cell across): fewest
        // total blobs, tie-broken by the blob closest to one cell. Restricting to token-bearing
        // thresholds keeps sub-cell noise specks on an empty table from being mistaken for the token.
        int best = -1, bestBlobs = int.MaxValue;
        double bestErr = double.MaxValue;
        for (int i = 0; i < sweep.Count; i++)
        {
            var radii = sweep[i].radiiPx;
            if (!HasTokenSizedBlob(radii, ppc)) continue;
            double err = ClosestToOneCellError(radii, ppc);
            if (radii.Length < bestBlobs || (radii.Length == bestBlobs && err < bestErr))
            {
                best = i; bestBlobs = radii.Length; bestErr = err;
            }
        }
        if (best < 0)
            return new AutoTuneResult { Success = false, Message = "no one-cell token found — place a single token on the map" };

        double minDia = double.MaxValue;
        foreach (var r in sweep[best].radiiPx)
            if (TokenSized(r)) minDia = Math.Min(minDia, DiaCells(r));
        minDia = Math.Floor(minDia * 100) / 100.0;
        double safeMinDiaFallback = Math.Max(0.3, minDia * 0.6);
        return new AutoTuneResult
        {
            Success = true,
            DiffThreshold = sweep[best].threshold,
            MinObjectCells = safeMinDiaFallback,
            TokenDiameterCells = minDia,
            BlobCount = bestBlobs,
            Message = $"sensitivity {sweep[best].threshold}, min size {safeMinDiaFallback:F2} cells — {bestBlobs} blobs (place exactly one token)"
        };
    }

    private static AutoTuneResult SelectArea(IReadOnlyList<(int threshold, double[] radiiPx)> sweep)
    {
        bool Qualifies(int i) => sweep[i].radiiPx.Length == 1;
        var (start, len) = LongestRun(sweep.Count, Qualifies);

        int idx, blobs;
        if (len > 0) { idx = start + len / 2; blobs = 1; }
        else
        {
            idx = -1;
            int bestBlobs = int.MaxValue;
            for (int i = 0; i < sweep.Count; i++)
            {
                int n = sweep[i].radiiPx.Length;
                if (n == 0) continue;
                if (n < bestBlobs) { bestBlobs = n; idx = i; }
            }
            if (idx < 0) return new AutoTuneResult { Success = false, Message = "no blobs at any sensitivity — nothing to tune" };
            blobs = bestBlobs;
        }

        double maxR = 0;
        foreach (var r in sweep[idx].radiiPx) maxR = Math.Max(maxR, r);
        double area = Math.PI * maxR * maxR;
        int minArea = Math.Clamp((int)Math.Round(area * 0.5), 200, 5000);
        return new AutoTuneResult
        {
            Success = true,
            DiffThreshold = sweep[idx].threshold,
            MinObjectCells = 0.0,
            MinBlobAreaPx = minArea,
            BlobCount = blobs,
            Message = blobs == 1
                ? $"sensitivity {sweep[idx].threshold}, min size {minArea}px (no grid)"
                : $"sensitivity {sweep[idx].threshold}, min size {minArea}px — {blobs} blobs (no grid)"
        };
    }

    private static bool HasTokenSizedBlob(double[] radii, float ppc)
    {
        foreach (var r in radii)
            if (2.0 * r / ppc >= 0.3) return true;
        return false;
    }

    // Closeness of the nearest blob to one cell in diameter (0 == exactly one cell across).
    private static double ClosestToOneCellError(double[] radii, float ppc)
    {
        double best = double.MaxValue;
        foreach (var r in radii) best = Math.Min(best, Math.Abs(2.0 * r / ppc - 1.0));
        return best;
    }

    /// <summary>Longest run of consecutive indices satisfying <paramref name="pred"/>.</summary>
    private static (int start, int len) LongestRun(int n, Func<int, bool> pred)
    {
        int bestStart = 0, bestLen = 0, curStart = 0, curLen = 0;
        for (int i = 0; i < n; i++)
        {
            if (pred(i))
            {
                if (curLen == 0) curStart = i;
                curLen++;
                if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
            }
            else curLen = 0;
        }
        return (bestStart, bestLen);
    }
}
