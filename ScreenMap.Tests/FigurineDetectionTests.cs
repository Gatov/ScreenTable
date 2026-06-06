using System.Drawing;
using System.Drawing.Drawing2D;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScreenMap.Vision;

namespace ScreenMap.Tests;

/// <summary>
/// Object (figurine) detection tests. FigurineDetector warps the camera frame onto the
/// reference player view via the four ArUco markers, diffs them, and reports blobs that
/// are present physically but not in the rendered map — the figurines.
///
/// The synthetic test exercises the marker→warp→diff→blob pipeline deterministically.
/// The real-pair test is Explicit: the captured reference predates the clean-render fix
/// (its map still has the detection overlay baked in), so it is kept only for inspection
/// until a fresh pair is captured.
/// </summary>
[TestFixture]
public class FigurineDetectionTests
{
    private const int Fid = 80;
    private const int Quiet = 12;
    private const int Ring = 8;
    private const float InsetFrac = 0.12f;

    /// <summary>A marked "screen": dark uniform background with the four fiducials laid out
    /// exactly like PlayersMap.DrawCornerFiducials — fractional-inset centers, black ring
    /// around a white quiet zone — so markers sit at the same fraction at any resolution.</summary>
    private static Bitmap RenderScene(int w, int h)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(80, 80, 80));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        var markers = ArucoMarkers.GetMarkers(Fid);
        int white = Fid + 2 * Quiet;
        int black = white + 2 * Ring;
        int half = black / 2;
        int fx = System.Math.Clamp((int)System.Math.Round(InsetFrac * w), half, w - half);
        int fy = System.Math.Clamp((int)System.Math.Round(InsetFrac * h), half, h - half);

        void DrawCentered(int cx, int cy, Bitmap m)
        {
            g.FillRectangle(Brushes.Black, cx - half, cy - half, black, black);
            g.FillRectangle(Brushes.White, cx - half + Ring, cy - half + Ring, white, white);
            g.DrawImage(m, cx - Fid / 2, cy - Fid / 2, Fid, Fid);
        }
        DrawCentered(fx, fy, markers[0]);
        DrawCentered(w - fx, fy, markers[1]);
        DrawCentered(w - fx, h - fy, markers[2]);
        DrawCentered(fx, h - fy, markers[3]);
        return bmp;
    }

    [Test]
    public void SyntheticObject_DetectedAsSingleBlob_InCorrectQuadrant()
    {
        // Reference: the clean rendered view (no object).
        using var view = RenderScene(960, 540);

        // Camera frame: the same scene at capture resolution, globally brighter (to exercise
        // brightness normalization), with one solid object the reference does not contain.
        using var cameraBgra = BitmapConverter.ToMat(RenderScene(1280, 720));
        using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);
        Cv2.Add(camera, new Scalar(40, 40, 40), camera); // camera is brighter than the reference
        var objCenter = new OpenCvSharp.Point(300, 360);  // left-center, clear of the corner fiducials
        Cv2.Circle(camera, objCenter, 26, new Scalar(0, 0, 0), -1); // a dark figurine vs the grey map

        using var detector = new FigurineDetector { MinBlobAreaPx = 200 };
        var status = detector.Detect(camera, view, out var dets);

        Assert.That(status, Is.EqualTo(DetectStatus.Ok));
        Assert.That(dets.Length, Is.EqualTo(1), "exactly one object expected");
        var d = dets[0];
        Assert.That(d.Center.X, Is.LessThan(view.Width / 2f), "object on the left");
    }

    [Test]
    public void ProduceCrops_IsolatesObject_AsCircularMaskedCrop()
    {
        using var view = RenderScene(960, 540);
        using var cameraBgra = BitmapConverter.ToMat(RenderScene(1280, 720));
        using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);
        Cv2.Add(camera, new Scalar(40, 40, 40), camera);
        var objCenter = new OpenCvSharp.Point(300, 360);
        Cv2.Circle(camera, objCenter, 26, new Scalar(0, 0, 0), -1);

        using var detector = new FigurineDetector { MinBlobAreaPx = 200, ProduceCrops = true };
        var status = detector.Detect(camera, view, out var dets);

        Assert.That(status, Is.EqualTo(DetectStatus.Ok));
        Assert.That(dets.Length, Is.EqualTo(1));
        var crops = detector.LastCrops;
        Assert.That(crops.Length, Is.EqualTo(1), "one crop per detection");

        var crop = crops[0];
        Assert.That(crop.Empty(), Is.False, "crop must hold pixels");
        Assert.That(crop.Channels(), Is.EqualTo(4), "BGRA: alpha carries the circular mask");
        var d = dets[0];
        // Square bounding box sized ~ 2*radius (clamped to image bounds).
        Assert.That(crop.Width, Is.InRange((int)(d.Radius * 2) - 4, (int)(d.Radius * 2) + 4));
        Assert.That(crop.Height, Is.EqualTo(crop.Width));
        // A corner pixel sits outside the inscribed disc → transparent (alpha 0); the center
        // sits inside → opaque (alpha 255).
        var corner = crop.At<Vec4b>(0, 0);
        Assert.That(corner.Item3, Is.EqualTo(0), "corner outside disc is transparent");
        var center = crop.At<Vec4b>(crop.Height / 2, crop.Width / 2);
        Assert.That(center.Item3, Is.EqualTo(255), "center inside disc is opaque");
    }

    [Test]
    public void NoObject_CleanScene_YieldsNoFalsePositives()
    {
        using var view = RenderScene(960, 540);
        using var cameraBgra = BitmapConverter.ToMat(RenderScene(1280, 720));
        using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);

        using var detector = new FigurineDetector { MinBlobAreaPx = 200 };
        detector.Detect(camera, view, out var dets);
        Assert.That(dets.Length, Is.EqualTo(0), "a matching scene must not produce phantom objects");
    }

    [Test]
    [Explicit("Diagnostic: marker counts across captured frames.")]
    public void CountMarkers_AllCaptures()
    {
        string dir = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
            "SampleFrames", "ScreenMapCaptures");
        using var d = OpenCvSharp.Aruco.CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
        var p = ArucoMarkers.CreateDetectorParameters();
        foreach (var f in System.IO.Directory.GetFiles(dir, "frame-*.png"))
        {
            using var im = Cv2.ImRead(f, ImreadModes.Color);
            OpenCvSharp.Aruco.CvAruco.DetectMarkers(im, d, out _, out var ids, p, out var rej);
            var idl = ids == null ? "" : string.Join(",", System.Linq.Enumerable.OrderBy(
                System.Linq.Enumerable.Select(ids, x => x), x => x));
            TestContext.WriteLine($"{System.IO.Path.GetFileName(f)} {im.Width}x{im.Height} markers={ids?.Length ?? 0} ids=[{idl}] rejected={rej?.Length ?? 0}");
        }
    }

    private static (Mat frame, Bitmap view) LoadCapture(string stamp)
    {
        string dir = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
            "SampleFrames", "ScreenMapCaptures");
        var frame = Cv2.ImRead(System.IO.Path.Combine(dir, $"frame-{stamp}.png"), ImreadModes.Color);
        var view = (Bitmap)Image.FromFile(System.IO.Path.Combine(dir, $"view-{stamp}.png"));
        return (frame, view);
    }

    // Real captured pair (post fractional-fiducial fix), no figurine on the table.
    // The whole map is bright, busy, filmed at an angle — a stress test for false positives.
    [Test]
    public void RealPair_171645_NoObject_NoFalsePositives()
    {
        var (frame, view) = LoadCapture("20260531-171645");
        using (frame)
        using (view)
        using (var detector = new FigurineDetector())
        {
            var status = detector.Detect(frame, view, out var dets);
            Assert.That(status, Is.EqualTo(DetectStatus.Ok));
            Assert.That(dets.Length, Is.EqualTo(0), "no figurine on the table");
        }
    }

    // Real captured pair with one dark token at top-center, bright map, heavy glare, and a
    // steeply angled top-left fiducial (recovered by the widened adaptive-threshold range).
    [Test]
    public void RealPair_185301_OneObject_TopCenter()
    {
        var (frame, view) = LoadCapture("20260531-185301");
        using (frame)
        using (view)
        // Heavy glare leaves a borderline bloom blob at the default sensitivity; a stricter
        // threshold (the adjustable sensitivity) isolates the real token cleanly.
        using (var detector = new FigurineDetector { DiffThreshold = 80, MinBlobAreaPx = 1500 })
        {
            var status = detector.Detect(frame, view, out var dets);
            Assert.That(status, Is.EqualTo(DetectStatus.Ok));
            Assert.That(dets.Length, Is.EqualTo(1), "exactly the one token");
            var d = dets[0];
            Assert.That(d.Center.X, Is.InRange(view.Width * 0.35f, view.Width * 0.65f), "top-center: horizontally centered");
            Assert.That(d.Center.Y, Is.LessThan(view.Height * 0.35f), "top-center: near the top");
        }
    }
}
