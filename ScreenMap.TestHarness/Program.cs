using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using ScreenMap.Vision;

namespace ScreenMap.TestHarness;

/// <summary>
/// Autonomous test harness for the ScreenMap detection pipeline.
///
/// Displays a rendered map scene (with ArUco markers) on the screen, captures from
/// an overhead camera, runs the full detect pipeline, and outputs structured JSON +
/// diagnostic images. Designed to be invoked by an automated code-improvement agent.
///
/// Usage:
///   ScreenMap.TestHarness.exe [options]
///
///   --camera  &lt;index&gt;    Camera device index (default: 1)
///   --settle  &lt;frames&gt;   Frames to discard for camera settling (default: 30)
///   --output  &lt;dir&gt;      Directory for output images/JSON (default: ./output)
///   --maps    &lt;dir&gt;      Directory containing sample map images (default: ./maps)
///   --all                Run detection on ALL maps in the directory (default: first only)
///   --loops   &lt;n&gt;        Capture→detect cycles per map (default: 1)
///   --display &lt;n&gt;        Screen index to display on (default: 0 = primary)
///   --json               Print DetectionResult JSON to stdout
/// </summary>
internal static class Program
{
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp" };

    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // --- Parse CLI args ---
        int cameraIndex = GetIntArg(args, "--camera", 1);
        int settleFrames = GetIntArg(args, "--settle", 30);
        string outputDir = GetStringArg(args, "--output", Path.Combine(AppContext.BaseDirectory, "output"));
        string mapsDir = GetStringArg(args, "--maps", Path.Combine(AppContext.BaseDirectory, "maps"));
        bool runAll = args.Contains("--all", StringComparer.OrdinalIgnoreCase);
        int loops = GetIntArg(args, "--loops", 1);
        int displayIndex = GetIntArg(args, "--display", 0);
        bool jsonOut = args.Contains("--json", StringComparer.OrdinalIgnoreCase);

        // --- Find map images ---
        var mapFiles = FindMapImages(mapsDir);
        if (!runAll && mapFiles.Count > 0)
            mapFiles = new List<string> { mapFiles[0] };

        // --- Set up output ---
        Directory.CreateDirectory(outputDir);

        // --- Open camera ---
        Console.Error.WriteLine($"Opening camera {cameraIndex}...");
        ICameraSource camera;
        try
        {
            camera = new OpenCvCameraSource(cameraIndex);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: Cannot open camera {cameraIndex}: {ex.Message}");
            return 2;
        }
        if (!camera.IsOpen)
        {
            camera.Dispose();
            Console.Error.WriteLine($"FATAL: Camera {cameraIndex} is not available.");
            return 2;
        }
        Console.Error.WriteLine($"Camera {cameraIndex} opened ({camera.GetType().Name}).");

        // --- Select display screen ---
        var screens = Screen.AllScreens;
        if (displayIndex < 0 || displayIndex >= screens.Length)
        {
            Console.Error.WriteLine($"WARNING: Display index {displayIndex} out of range (0..{screens.Length - 1}), using primary.");
            displayIndex = 0;
        }
        var targetScreen = screens[displayIndex];
        Console.Error.WriteLine($"Displaying on screen {displayIndex}: {targetScreen.Bounds.Width}x{targetScreen.Bounds.Height} @ ({targetScreen.Bounds.X},{targetScreen.Bounds.Y})");

        // --- Create display form ---
        var displayForm = new DisplayForm();
        displayForm.GoFullscreen(targetScreen);
        displayForm.Show();
        Application.DoEvents(); // force the form to paint once

        // --- Run ---
        var runner = new TestRunner(settleFrames, outputDir);
        var allResults = new List<DetectionResult>();
        int runId = 0;
        bool anyFailure = false;

        try
        {
            foreach (var mapFile in mapFiles)
            {
                string mapName = Path.GetFileName(mapFile);
                Console.Error.WriteLine($"\n=== Map: {mapName} ===");

                using var mapBitmap = mapFile != null ? (Bitmap)Image.FromFile(mapFile) : null;

                // We'll run 1 random test position per map
                int testPositions = 1;
                for (int loop = 0; loop < testPositions; loop++)
                {
                    runId++;
                    int seed = Environment.TickCount + runId;
                    var rng = new Random(seed);

                    // Render a random view of this map
                    var sceneSize = new Size(targetScreen.Bounds.Width, targetScreen.Bounds.Height);
                    using var baseScene = TestSceneRenderer.RenderScene(sceneSize, mapBitmap, rng);

                    // Create a scene with a mock figurine overlay to display
                    using var displayedScene = (Bitmap)baseScene.Clone();
                    int margin = 250;
                    float mockRadius = 40;
                    float mockX = rng.Next(margin, Math.Max(margin + 1, sceneSize.Width - margin));
                    float mockY = rng.Next(margin, Math.Max(margin + 1, sceneSize.Height - margin));
                    string posName = "Random";

                    using (var g = Graphics.FromImage(displayedScene))
                    {
                        // Draw a highly contrasting white circle with black outline to guarantee luma diff
                        using var brush = new SolidBrush(Color.White);
                        using var pen = new Pen(Color.Black, 6);
                        g.FillEllipse(brush, mockX - mockRadius, mockY - mockRadius, mockRadius * 2, mockRadius * 2);
                        g.DrawEllipse(pen, mockX - mockRadius, mockY - mockRadius, mockRadius * 2, mockRadius * 2);
                    }

                    // Display it
                    displayForm.SetScene((Bitmap)displayedScene.Clone());
                    Application.DoEvents();

                    // Brief pause to let the screen update and the camera see it
                    System.Threading.Thread.Sleep(500);

                    // Run detection using the clean baseScene as reference
                    var result = runner.RunCycle(camera, baseScene, runId, mapName, seed, new PointF(mockX, mockY), mockRadius);
                    allResults.Add(result);

                    // Verify if the detected figurine matches the mock position
                    bool positionCorrect = false;
                    foreach (var f in result.Figurines)
                    {
                        double dist = Math.Sqrt(Math.Pow(f.CenterX - mockX, 2) + Math.Pow(f.CenterY - mockY, 2));
                        if (dist < 50) // 50px tolerance
                        {
                            positionCorrect = true;
                            break;
                        }
                    }

                    // Report
                    string status = result.MarkersDetected ? "OK" : "MARKERS_MISSING";
                    if (!string.IsNullOrEmpty(result.ErrorMessage)) status = "ERROR";
                    else if (result.MarkersDetected) status = positionCorrect ? "OK_MATCH" : "POS_MISMATCH";

                    Console.Error.WriteLine(
                        $"  Run {runId} [{posName}]: {status} | markers={result.MarkerCount}/4 | " +
                        $"figurines={result.FigurineCount} (target: {mockX:F0},{mockY:F0}) | {result.ProcessingMs:F0}ms");

                    if (!result.MarkersDetected || !string.IsNullOrEmpty(result.ErrorMessage) || !positionCorrect)
                        anyFailure = true;
                }
            }

            if (mapFiles.Count == 0)
            {
                // No maps — run with solid background
                Console.Error.WriteLine("\nNo map images found, running with solid background.");
                runId++;
                int seed = Environment.TickCount + runId;
                var rng = new Random(seed);
                var sceneSize = new Size(targetScreen.Bounds.Width, targetScreen.Bounds.Height);
                using var scene = TestSceneRenderer.RenderScene(sceneSize, null, rng);

                displayForm.SetScene((Bitmap)scene.Clone());
                Application.DoEvents();
                System.Threading.Thread.Sleep(500);

                var result = runner.RunCycle(camera, scene, runId, "(no map)", seed);
                allResults.Add(result);

                if (!result.MarkersDetected || !string.IsNullOrEmpty(result.ErrorMessage))
                    anyFailure = true;
            }
        }
        finally
        {
            camera.Dispose();
            displayForm.Dispose();
        }

        // --- Output ---
        var summary = new
        {
            Timestamp = DateTime.UtcNow.ToString("o"),
            CameraIndex = cameraIndex,
            TotalRuns = allResults.Count,
            AllMarkersDetected = allResults.All(r => r.MarkersDetected),
            Results = allResults,
        };

        string json = JsonConvert.SerializeObject(summary, Formatting.Indented);

        // Always write to file
        var summaryPath = Path.Combine(outputDir, "results.json");
        File.WriteAllText(summaryPath, json);
        Console.Error.WriteLine($"\nResults written to {summaryPath}");

        // Optionally print to stdout
        if (jsonOut)
            Console.WriteLine(json);

        return anyFailure ? 1 : 0;
    }

    private static List<string> FindMapImages(string dir)
    {
        if (!Directory.Exists(dir))
        {
            Console.Error.WriteLine($"Maps directory not found: {dir}");
            return new List<string>();
        }
        var files = Directory.GetFiles(dir)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToList();
        Console.Error.WriteLine($"Found {files.Count} map image(s) in {dir}");
        return files;
    }

    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && int.TryParse(args[i + 1], out var val))
                return val;
        return defaultValue;
    }

    private static string GetStringArg(string[] args, string name, string defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        return defaultValue;
    }
}
