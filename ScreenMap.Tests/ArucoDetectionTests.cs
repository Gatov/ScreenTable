using System.Drawing;
using System.Drawing.Drawing2D;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using ScreenMap.Logic.Camera;

namespace ScreenMap.Tests;

/// <summary>
/// Detection tests for the ArUco corner fiducials. These isolate each stage of the
/// pipeline — marker generation, the clean rendered player view, and a simulated camera
/// capture — so a detection failure can be attributed to a specific stage rather than
/// guessed at. The resolution sweep reproduces the real-world "0/4 on a 4K camera"
/// failure: forcing the capture down to 720p shrinks an 80px fiducial below the size
/// the detector can decode.
/// </summary>
[TestFixture]
public class ArucoDetectionTests
{
    private Dictionary _dict;
    private DetectorParameters _params;

    [SetUp]
    public void SetUp()
    {
        _dict = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
        _params = ArucoMarkers.CreateDetectorParameters(); // the shipping params
    }

    [TearDown]
    public void TearDown() => _dict?.Dispose();

    private int Detect(Mat img, out int rejectedCount)
    {
        CvAruco.DetectMarkers(img, _dict, out _, out var ids, _params, out var rejected);
        rejectedCount = rejected?.Length ?? 0;
        return ids?.Length ?? 0;
    }

    private int Detect(Mat img) => Detect(img, out _);

    /// <summary>Renders a player-view-style image: dark background, white quiet zones,
    /// the four ArUco fiducials in the corners — mirrors PlayersMap.DrawCornerFiducials.</summary>
    private static Mat RenderScreen(int w, int h, int fid, int pad)
    {
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(40, 40, 40));
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.CompositingMode = CompositingMode.SourceCopy;
            var markers = ArucoMarkers.GetMarkers(fid);
            using (var white = new SolidBrush(Color.White))
            {
                int q = fid + pad;
                g.FillRectangle(white, 0, 0, q, q);
                g.FillRectangle(white, w - q, 0, q, q);
                g.FillRectangle(white, w - q, h - q, q, q);
                g.FillRectangle(white, 0, h - q, q, q);
            }
            g.DrawImage(markers[0], 4, 4, fid, fid);
            g.DrawImage(markers[1], w - fid - 4, 4, fid, fid);
            g.DrawImage(markers[2], w - fid - 4, h - fid - 4, fid, fid);
            g.DrawImage(markers[3], 4, h - fid - 4, fid, fid);
        }
        return BitmapConverter.ToMat(bmp);
    }

    /// <summary>Simulates a camera filming the screen: the screen occupies <paramref name="fillFrac"/>
    /// of a <paramref name="frameW"/>×<paramref name="frameH"/> capture, at a slight perspective,
    /// with backlight glare lifting the blacks and a little blur.</summary>
    private static Mat SimulateFilm(Mat screen, int frameW, int frameH, double fillFrac,
        double blur = 1.0, int angleOffset = 8, double glare = 25)
    {
        var frame = new Mat(frameH, frameW, MatType.CV_8UC3, new Scalar(70, 70, 70));
        double mw = frameW * fillFrac, mh = mw * screen.Height / screen.Width;
        double ox = (frameW - mw) / 2, oy = (frameH - mh) / 2;
        var src = new[]
        {
            new Point2f(0, 0), new Point2f(screen.Width, 0),
            new Point2f(screen.Width, screen.Height), new Point2f(0, screen.Height)
        };
        var dst = new[]
        {
            new Point2f((float)(ox + angleOffset), (float)oy),
            new Point2f((float)(ox + mw), (float)(oy + angleOffset)),
            new Point2f((float)(ox + mw - angleOffset), (float)(oy + mh)),
            new Point2f((float)ox, (float)(oy + mh - angleOffset)),
        };
        using var h = Cv2.GetPerspectiveTransform(src, dst);
        using var scr3 = screen.Channels() == 4 ? screen.CvtColor(ColorConversionCodes.BGRA2BGR) : screen.Clone();
        Cv2.WarpPerspective(scr3, frame, h, new OpenCvSharp.Size(frameW, frameH),
            InterpolationFlags.Linear, BorderTypes.Transparent);
        if (glare > 0) Cv2.Add(frame, new Scalar(glare, glare, glare), frame);
        if (blur > 0) { int k = (int)blur * 2 + 1; Cv2.GaussianBlur(frame, frame, new OpenCvSharp.Size(k, k), 0); }
        return frame;
    }

    [Test]
    public void Markers_AreValidDict4x4_50()
    {
        // A single generated marker with a generous quiet zone must decode.
        using var canvas = new Mat(480, 480, MatType.CV_8UC1, new Scalar(255));
        using var m = new Mat();
        using (var d = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName))
            d.GenerateImageMarker(ArucoMarkers.CornerIds[0], 200, m, 1);
        using (var roi = new Mat(canvas, new Rect(140, 140, 200, 200))) m.CopyTo(roi);
        Assert.That(Detect(canvas), Is.EqualTo(1));
    }

    [Test]
    public void CleanRender_CurrentSettings_DetectsAllFour()
    {
        // The rendered player view (fid=80, pad=8 — the current production values) is
        // perfectly detectable. So generation/placement are not the problem.
        using var screen = RenderScreen(1280, 720, fid: 80, pad: 8);
        Assert.That(Detect(screen), Is.EqualTo(4));
    }

    [Test]
    public void SimulatedFilm_ReasonableDistance_DetectsAllFour()
    {
        // Screen fills 70% of a 720p frame, marker stays ~40px: detection survives.
        using var screen = RenderScreen(1280, 720, fid: 80, pad: 8);
        using var film = SimulateFilm(screen, 1280, 720, fillFrac: 0.7);
        Assert.That(Detect(film), Is.EqualTo(4));
    }

    // ---- Resolution sweep: reproduces the 4K-camera-forced-to-720p failure ----
    //
    // The player display is high-resolution (e.g. 4K). The on-screen fiducial is 80px,
    // so it is a tiny fraction of the display. When the capture is forced down to 720p
    // the fiducial lands at ~24px with a sub-pixel quiet zone and can no longer be
    // decoded. Capturing at higher resolution restores the pixels the detector needs.

    [TestCase(1280, 720, 0.9, TestName = "Capture720p_fill90")]
    [TestCase(1920, 1080, 0.9, TestName = "Capture1080p_fill90")]
    [TestCase(3840, 2160, 0.9, TestName = "Capture4k_fill90")]
    public void ResolutionSweep_4kDisplay_GoodFraming(int frameW, int frameH, double fill)
    {
        using var screen = RenderScreen(3840, 2160, fid: 80, pad: 8);
        using var film = SimulateFilm(screen, frameW, frameH, fillFrac: fill);
        int found = Detect(film, out int rejected);
        TestContext.WriteLine($"capture {frameW}x{frameH} fill={fill}: detected={found}/4 rejected={rejected}");
        Assert.That(found, Is.EqualTo(4), $"expected all 4 at {frameW}x{frameH}");
    }

    // Exploratory matrix: marker pixel count (hence detection) falls off as the screen
    // fills less of the frame, and the cliff arrives much sooner at low capture
    // resolution. Printed for insight; not asserted.
    [TestCase(1280, 720)]
    [TestCase(3840, 2160)]
    public void FramingFalloff_Matrix(int frameW, int frameH)
    {
        using var screen = RenderScreen(3840, 2160, fid: 80, pad: 8);
        foreach (var fill in new[] { 0.9, 0.6, 0.4, 0.3, 0.2 })
        {
            using var film = SimulateFilm(screen, frameW, frameH, fillFrac: fill);
            int found = Detect(film, out int rej);
            int markerPx = (int)(frameW * fill * 80.0 / 3840.0);
            TestContext.WriteLine($"{frameW}x{frameH} fill={fill:0.0} (~{markerPx}px marker): detected={found}/4 rejected={rej}");
        }
    }

    // ---- Real captured frame (the actual failing case) ----

    private static string SampleFramePath =>
        System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "SampleFrames",
            "aruco-frame-20260531-121638.png");

    [Test]
    public void RealFrame_720pCapture_FailsAndIsNotRecoverableInSoftware()
    {
        // The actual failing capture: a 4K camera forced to 1280x720. The four fiducials
        // land at ~30px and soft, so the detector finds the squares (rejected>0) but
        // cannot decode the 4x4 bits. This is the root-cause evidence. It is NOT fixable
        // by params or upscaling — the pixels are simply not there — which is why the fix
        // is to capture at the camera's native resolution.
        using var frame = Cv2.ImRead(SampleFramePath, ImreadModes.Color);
        Assert.That(frame.Empty(), Is.False, "sample frame missing: " + SampleFramePath);
        Assert.That((frame.Width, frame.Height), Is.EqualTo((1280, 720)));

        int found = Detect(frame, out int rej);
        TestContext.WriteLine($"real 720p frame: detected={found}/4 rejected={rej}");
        Assert.That(found, Is.LessThan(4), "720p capture cannot decode the fiducials");

        using var up = new Mat();
        Cv2.Resize(frame, up, new OpenCvSharp.Size(frame.Width * 2, frame.Height * 2), 0, 0, InterpolationFlags.Cubic);
        int upFound = Detect(up, out _);
        TestContext.WriteLine($"upscaled 2x: detected={upFound}/4");
        Assert.That(upFound, Is.LessThan(4), "upscaling cannot recover lost detail");
    }

    // Real 4K frame captured after the fix (native-resolution capture) — the fiducials now
    // have enough pixels to decode on a bright, busy map filmed at an angle. Proves the
    // capture fix + the black-ring fiducials on real hardware.
    [TestCase("ScreenMapCaptures/frame-20260531-171645.png")]
    public void RealFrame_4kCapture_DetectsAllFour(string rel)
    {
        var path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "SampleFrames",
            rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
        using var frame = Cv2.ImRead(path, ImreadModes.Color);
        Assert.That(frame.Empty(), Is.False, "sample frame missing: " + path);
        Assert.That((frame.Width, frame.Height), Is.EqualTo((3840, 2160)));

        int found = Detect(frame, out int rej);
        TestContext.WriteLine($"real 4K frame {rel}: detected={found}/4 rejected={rej}");
        Assert.That(found, Is.EqualTo(4));
    }

    [Test]
    public void HigherResolution_WidensUsableFraming()
    {
        // At moderately tight framing (screen 40% of frame) an 80px fiducial is ~10px at
        // 720p and undetectable, but ~32px at 4K and decodes cleanly. This is the core
        // reason for capturing at the camera's native resolution instead of forcing 720p.
        using var screen = RenderScreen(3840, 2160, fid: 80, pad: 8);
        using var film720 = SimulateFilm(screen, 1280, 720, fillFrac: 0.4);
        using var film4K = SimulateFilm(screen, 3840, 2160, fillFrac: 0.4);
        int at720 = Detect(film720);
        int at4K = Detect(film4K);
        TestContext.WriteLine($"fill=0.4 → 720p={at720}/4, 4k={at4K}/4");
        Assert.That(at4K, Is.GreaterThan(at720), "native resolution should tolerate tighter framing");
        Assert.That(at4K, Is.EqualTo(4));
    }
}
