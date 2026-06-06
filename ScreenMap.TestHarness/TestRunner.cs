using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using ScreenMap.Vision;

namespace ScreenMap.TestHarness;

/// <summary>
/// Runs a single capture→detect cycle against a live camera and a displayed reference
/// scene. Produces a <see cref="DetectionResult"/> with diagnostic images.
/// </summary>
public class TestRunner
{
    private readonly int _settleFrames;
    private readonly string _outputDir;

    public TestRunner(int settleFrames, string outputDir)
    {
        _settleFrames = settleFrames;
        _outputDir = outputDir;
    }

    /// <summary>
    /// Runs one capture→detect cycle.
    /// </summary>
    /// <param name="camera">Open camera source.</param>
    /// <param name="referenceScene">The bitmap currently displayed on the screen (with markers).</param>
    /// <param name="runId">Monotonic run identifier.</param>
    /// <param name="mapName">Name of the map file used.</param>
    /// <param name="randomSeed">The seed used to generate the scene crop.</param>
    public DetectionResult RunCycle(ICameraSource camera, Bitmap referenceScene,
        int runId, string mapName, int randomSeed, PointF? expectedFigurine = null, float expectedRadius = 0)
    {
        var result = new DetectionResult
        {
            RunId = runId,
            MapName = mapName,
            RandomSeed = randomSeed,
        };

        try
        {
            // --- Stabilize: discard N frames to let camera auto-exposure settle ---
            // TODO: Consider diff-based stabilization (compare consecutive frames until
            // the difference drops below a threshold) if simple frame-discard proves
            // insufficient for reliable detection.
            Mat frame = null;
            for (int i = 0; i < _settleFrames; i++)
            {
                if (!camera.TryGrab(out frame))
                {
                    result.ErrorMessage = $"Camera grab failed during settling (frame {i}/{_settleFrames})";
                    result.DetectStatus = "GrabFailed";
                    return result;
                }
            }

            if (frame == null || frame.Empty())
            {
                result.ErrorMessage = "No frame captured after settling";
                result.DetectStatus = "Empty";
                return result;
            }

            // Clone the frame since TryGrab reuses the buffer
            using var capturedFrame = frame.Clone();

            // Save raw frame
            string runDir = null;
            if (_outputDir != null)
            {
                runDir = Path.Combine(_outputDir, $"run-{runId:D4}");
                Directory.CreateDirectory(runDir);

                var rawPath = Path.Combine(runDir, "raw-frame.png");
                Cv2.ImWrite(rawPath, capturedFrame);
                result.RawFramePath = Path.GetRelativePath(_outputDir, rawPath);

                // Reference scene will be saved after detection to include overlays
            }

            // --- AutoTuner Check ---
            var tuner = new AutoTuner();
            // Assume the frame width spans about 30 cells to test grid-aware tuning
            float simulatedPpc = capturedFrame.Width / 30f;
            var tuneResult = tuner.Tune(capturedFrame, referenceScene, pixelsPerCell: simulatedPpc);
            Console.Error.WriteLine($"    [AutoTuner] Success: {tuneResult.Success}, Thresh: {tuneResult.DiffThreshold}, " +
                                    $"Blobs: {tuneResult.BlobCount}, MinCells: {tuneResult.MinObjectCells:F1}, Msg: {tuneResult.Message}");

            // --- Count markers found (for diagnostics) ---
            using var dict = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
            var detParams = ArucoMarkers.CreateDetectorParameters();
            CvAruco.DetectMarkers(capturedFrame, dict, out _, out var ids, detParams, out _);
            result.MarkerCount = ids?.Length ?? 0;
            result.MarkersDetected = result.MarkerCount >= ArucoMarkers.MarkerCount;

            // --- Run the detection pipeline ---
            using var detector = new FigurineDetector { ProduceCrops = true };
            if (tuneResult.Success)
            {
                detector.DiffThreshold = tuneResult.DiffThreshold;
                detector.MinObjectCells = tuneResult.MinObjectCells;
                detector.PixelsPerCell = simulatedPpc;
            }
            
            var sw = Stopwatch.StartNew();
            var status = detector.Detect(capturedFrame, referenceScene, out var detections);
            sw.Stop();

            result.ProcessingMs = sw.Elapsed.TotalMilliseconds;
            result.DetectStatus = status.ToString();
            result.FigurineCount = detections.Length;
            result.Figurines = detections.Select(d => new DetectionResult.FigurineInfo
            {
                CenterX = d.Center.X,
                CenterY = d.Center.Y,
                Radius = d.Radius,
            }).ToArray();

            // --- Save annotated frame and reference scene ---
            if (runDir != null)
            {
                var annotated = BuildAnnotatedImage(capturedFrame, detections, ids, result);
                var annotatedPath = Path.Combine(runDir, "annotated.png");
                Cv2.ImWrite(annotatedPath, annotated);
                annotated.Dispose();
                result.AnnotatedFramePath = Path.GetRelativePath(_outputDir, annotatedPath);

                using var annotatedRef = (Bitmap)referenceScene.Clone();
                using (var g = Graphics.FromImage(annotatedRef))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    if (expectedFigurine.HasValue)
                    {
                        using var pinkBrush = new SolidBrush(Color.FromArgb(128, 255, 20, 147)); // DeepPink 50%
                        float r = expectedRadius;
                        g.FillEllipse(pinkBrush, expectedFigurine.Value.X - r, expectedFigurine.Value.Y - r, r * 2, r * 2);
                    }
                    if (detections != null)
                    {
                        using var blueBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 255)); // Blue 50%
                        foreach (var d in detections)
                        {
                            g.FillEllipse(blueBrush, d.Center.X - d.Radius, d.Center.Y - d.Radius, d.Radius * 2, d.Radius * 2);
                        }
                    }
                }
                var refPath = Path.Combine(runDir, "reference-scene.png");
                annotatedRef.Save(refPath, ImageFormat.Png);
                result.ReferenceScenePath = Path.GetRelativePath(_outputDir, refPath);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.ToString();
            result.DetectStatus = "Error";
        }

        return result;
    }

    /// <summary>
    /// Draws detection circles and marker info onto the camera frame for diagnostics.
    /// </summary>
    private static Mat BuildAnnotatedImage(Mat frame, FigurineDetection[] detections,
        int[] markerIds, DetectionResult result)
    {
        var annotated = frame.Clone();

        // Draw marker count info
        var color = result.MarkersDetected ? Scalar.LightGreen : Scalar.Red;
        Cv2.PutText(annotated,
            $"Markers: {result.MarkerCount}/4  Figurines: {result.FigurineCount}  {result.ProcessingMs:F0}ms",
            new OpenCvSharp.Point(20, 40), HersheyFonts.HersheySimplex, 1.0, color, 2);

        Cv2.PutText(annotated,
            $"Status: {result.DetectStatus}  Run: {result.RunId}",
            new OpenCvSharp.Point(20, 80), HersheyFonts.HersheySimplex, 0.8, Scalar.White, 2);

        // Draw detection circles
        foreach (var det in detections)
        {
            var center = new OpenCvSharp.Point((int)det.Center.X, (int)det.Center.Y);
            Cv2.Circle(annotated, center, (int)det.Radius, Scalar.LimeGreen, 3);
            Cv2.Circle(annotated, center, 4, Scalar.Red, -1);
        }

        return annotated;
    }
}
