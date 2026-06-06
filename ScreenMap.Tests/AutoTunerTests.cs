using System.Collections.Generic;
using System.Drawing;
using System.IO;
using NUnit.Framework;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using ScreenMap.Vision;

namespace ScreenMap.Tests;

/// <summary>
/// Tests for the auto-adjust parameter search. <see cref="AutoTuner.SelectThreshold"/> is the
/// pure pick step: given the blob radii each candidate threshold produced, choose the sensitivity
/// (and min size) that isolates the single placed token. Grid-aware (radii measured in cells)
/// with a no-grid pixel-area fallback.
/// </summary>
[TestFixture]
public class AutoTunerTests
{
    private static (int, double[]) Row(int threshold, params double[] radii) => (threshold, radii);

    [Test]
    public void SelectThreshold_Grid_PicksCenterOfSingleTokenPlateau()
    {
        const float ppc = 20f; // one cell = 20 px; a 1-cell-wide token has radius ~10 px (diameter 20)
        var sweep = new List<(int, double[])>
        {
            Row(20, 10, 3, 3),  // 3 blobs: token + sub-cell noise
            Row(25, 10, 3),     // 2 blobs
            Row(30, 10),        // exactly the token (1 cell across)
            Row(35, 10.5),      // exactly the token
            Row(40, 10),        // exactly the token
            Row(45),            // nothing
        };

        var r = AutoTuner.SelectThreshold(sweep, ppc);

        Assert.That(r.Success, Is.True);
        Assert.That(r.DiffThreshold, Is.EqualTo(35), "center of the 30..40 single-token plateau");
        Assert.That(r.MinObjectCells, Is.EqualTo(1.0), "token is one cell across");
        Assert.That(r.BlobCount, Is.EqualTo(1));
        Assert.That(r.MinBlobAreaPx, Is.EqualTo(0), "px area is unused when the grid scale is known");
    }

    [Test]
    public void SelectThreshold_Grid_NoCleanSingle_FallsBackButStillSucceeds()
    {
        const float ppc = 20f;
        var sweep = new List<(int, double[])>
        {
            Row(20, 10, 6),
            Row(25, 10.5, 5),
            Row(30, 10, 7),
        };

        var r = AutoTuner.SelectThreshold(sweep, ppc);

        Assert.That(r.Success, Is.True, "best-effort: always returns a recommendation");
        Assert.That(r.MinObjectCells, Is.EqualTo(1.0), "the token is still one cell across");
        Assert.That(r.Message, Does.Contain("blob"), "message flags the imperfect result");
    }

    [Test]
    public void SelectThreshold_NoGrid_UsesPixelAreaFallback()
    {
        var sweep = new List<(int, double[])>
        {
            Row(20, 30, 8),
            Row(25, 30),   // single blob, radius 30 -> area ~2827 px
            Row(30, 30),
            Row(35, 31),
            Row(40),
        };

        var r = AutoTuner.SelectThreshold(sweep, pixelsPerCell: 0f);

        Assert.That(r.Success, Is.True);
        Assert.That(r.DiffThreshold, Is.EqualTo(30), "center of the 25..35 single-blob plateau");
        Assert.That(r.MinObjectCells, Is.EqualTo(0.0), "cells are unknown without a grid scale");
        // Half the token's pixel area: pi * 31^2 * 0.5 ~= 1509, clamped to [200, 5000].
        Assert.That(r.MinBlobAreaPx, Is.InRange(1300, 1700));
    }

    [Test]
    public void SelectThreshold_EmptySweep_Aborts()
    {
        var r = AutoTuner.SelectThreshold(new List<(int, double[])>(), pixelsPerCell: 20f);
        Assert.That(r.Success, Is.False);
    }

    private static (Mat frame, Bitmap view) LoadCapture(string stamp)
    {
        string dir = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "SampleFrames", "ScreenMapCaptures");
        var frame = Cv2.ImRead(Path.Combine(dir, $"frame-{stamp}.png"), ImreadModes.Color);
        var view = (Bitmap)Image.FromFile(Path.Combine(dir, $"view-{stamp}.png"));
        return (frame, view);
    }

    [Test]
    public void Tune_RealPair_OneToken_NoGrid_FindsSingleBlob()
    {
        var (frame, view) = LoadCapture("20260531-185301");
        using (frame)
        using (view)
        {
            var r = new AutoTuner().Tune(frame, view, pixelsPerCell: 0f);

            Assert.That(r.Success, Is.True);
            Assert.That(r.MinObjectCells, Is.EqualTo(0.0), "no grid scale -> pixel-area mode");
            Assert.That(r.DiffThreshold, Is.InRange(AutoTuner.MinThreshold, AutoTuner.MaxThreshold));
            Assert.That(r.MinBlobAreaPx, Is.GreaterThan(200));

            // The whole point: applying the recommended params isolates exactly the one token.
            using var tuned = new FigurineDetector
            {
                DiffThreshold = r.DiffThreshold,
                MinBlobAreaPx = r.MinBlobAreaPx
            };
            tuned.Detect(frame, view, out var dets);
            Assert.That(dets.Length, Is.EqualTo(1), "recommended params leave just the token");
        }
    }

    [Test]
    public void Tune_RealPair_OneToken_Grid_SetsOneCell()
    {
        var (frame, view) = LoadCapture("20260531-185301");
        using (frame)
        using (view)
        {
            // Measure the token's radius once, then set the cell scale so the token is one cell
            // across (diameter == 2 * radius).
            using var probe = new FigurineDetector { DiffThreshold = 80, MinBlobAreaPx = 1500 };
            probe.Detect(frame, view, out var dets);
            Assert.That(dets.Length, Is.EqualTo(1), "fixture has exactly one token");
            float ppc = dets[0].Radius * 2f;

            var r = new AutoTuner().Tune(frame, view, ppc);

            Assert.That(r.Success, Is.True);
            Assert.That(r.MinObjectCells, Is.EqualTo(1.0), "the placed token is one cell across");
            Assert.That(r.DiffThreshold, Is.InRange(AutoTuner.MinThreshold, AutoTuner.MaxThreshold));
        }
    }

    [Test]
    public void Tune_CleanScene_NoObject_ReportsNoToken()
    {
        // Reference and camera show the same clean scene (markers, no object). Markers align, but
        // there is no token-sized blob anywhere -> the tuner reports there is nothing to tune.
        using var view = TestSceneRenderer.RenderScene(new System.Drawing.Size(960, 540));
        using var cameraBgra = BitmapConverter.ToMat(TestSceneRenderer.RenderScene(new System.Drawing.Size(1280, 720)));
        using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);

        var r = new AutoTuner().Tune(camera, view, pixelsPerCell: 40f);

        Assert.That(r.Success, Is.False, "no object on the table -> nothing to tune");
        Assert.That(r.Message, Does.Contain("token"));
    }

    [Test]
    public void Tune_MockFigurine_ReportsSuccess()
    {
        var size = new System.Drawing.Size(960, 540);
        using var view = TestSceneRenderer.RenderScene(size);
        using var cameraView = (Bitmap)view.Clone();
        using (var g = System.Drawing.Graphics.FromImage(cameraView))
        {
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.Black, 6);
            int r = 20;
            g.FillEllipse(brush, 480 - r, 270 - r, r * 2, r * 2);
            g.DrawEllipse(pen, 480 - r, 270 - r, r * 2, r * 2);
        }

        using var cameraBgra = BitmapConverter.ToMat(cameraView);
        using var camera = cameraBgra.CvtColor(ColorConversionCodes.BGRA2BGR);

        // 1. Without Grid (Pixel Area mode)
        var rNoGrid = new AutoTuner().Tune(camera, view, pixelsPerCell: 0f);
        Assert.That(rNoGrid.Success, Is.True, "NoGrid: AutoTuner failed to find the mock figurine");
        Assert.That(rNoGrid.BlobCount, Is.EqualTo(1));

        // 2. With Grid
        var rGrid = new AutoTuner().Tune(camera, view, pixelsPerCell: 20f);
        Assert.That(rGrid.Success, Is.True, "Grid: AutoTuner failed to find the mock figurine");
        Assert.That(rGrid.BlobCount, Is.EqualTo(1));
    }
}
