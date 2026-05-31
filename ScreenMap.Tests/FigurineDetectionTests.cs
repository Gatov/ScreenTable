using System.Drawing;
using System.Drawing.Drawing2D;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScreenMap.Logic.Camera;

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

    /// <summary>A marked "screen": dark uniform background with the four corner fiducials
    /// and their white quiet zones — same layout PlayersMap renders.</summary>
    private static Bitmap RenderScene(int w, int h)
    {
        var bmp = new Bitmap(w, h);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.FromArgb(80, 80, 80));
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;
        var markers = ArucoMarkers.GetMarkers(Fid);
        using (var white = new SolidBrush(Color.White))
        {
            int q = Fid + 8;
            g.FillRectangle(white, 0, 0, q, q);
            g.FillRectangle(white, w - q, 0, q, q);
            g.FillRectangle(white, w - q, h - q, q, q);
            g.FillRectangle(white, 0, h - q, q, q);
        }
        g.DrawImage(markers[0], 4, 4, Fid, Fid);
        g.DrawImage(markers[1], w - Fid - 4, 4, Fid, Fid);
        g.DrawImage(markers[2], w - Fid - 4, h - Fid - 4, Fid, Fid);
        g.DrawImage(markers[3], 4, h - Fid - 4, Fid, Fid);
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
        var objCenter = new OpenCvSharp.Point(220, 560);  // bottom-left region
        Cv2.Circle(camera, objCenter, 26, new Scalar(0, 0, 0), -1); // a dark figurine vs the grey map

        using var detector = new FigurineDetector { MinBlobAreaPx = 200 };
        var status = detector.Detect(camera, view, out var dets);

        Assert.That(status, Is.EqualTo(DetectStatus.Ok));
        Assert.That(dets.Length, Is.EqualTo(1), "exactly one object expected");
        var d = dets[0];
        Assert.That(d.Center.X, Is.LessThan(view.Width / 2f), "object on the left");
        Assert.That(d.Center.Y, Is.GreaterThan(view.Height / 2f), "object on the bottom");
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

    /// <summary>
    /// Once a clean pair is captured (post overlay-split fix), drop it in
    /// SampleFrames/ScreenMapCaptures and assert here: load frame + view, run Detect,
    /// expect the real figurine. Kept as a hook for that next step.
    /// </summary>
    [Test]
    [Explicit("Add a freshly captured frame/view pair to assert against.")]
    public void RealPair_FromCaptures()
    {
        string dir = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
            "SampleFrames", "ScreenMapCaptures");
        var frames = System.IO.Directory.Exists(dir)
            ? System.IO.Directory.GetFiles(dir, "frame-*.png")
            : System.Array.Empty<string>();
        Assert.That(frames, Is.Not.Empty, "no captured frames in " + dir);
        foreach (var fp in frames)
        {
            var stamp = System.IO.Path.GetFileNameWithoutExtension(fp).Substring("frame-".Length);
            var vp = System.IO.Path.Combine(dir, $"view-{stamp}.png");
            if (!System.IO.File.Exists(vp)) continue;
            using var frame = Cv2.ImRead(fp, ImreadModes.Color);
            using var view = (Bitmap)Image.FromFile(vp);
            using var detector = new FigurineDetector();
            var status = detector.Detect(frame, view, out var dets);
            TestContext.WriteLine($"{stamp}: status={status} detections={dets.Length}");
        }
    }
}
