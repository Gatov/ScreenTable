using System;
using Newtonsoft.Json;

namespace ScreenMap.Vision;

/// <summary>
/// Structured result of a single capture→detect cycle, designed to be consumed by
/// an automated code-improvement agent. Serialized as JSON to stdout and/or file.
/// </summary>
public class DetectionResult
{
    /// <summary>Monotonically increasing run identifier within this session.</summary>
    public int RunId { get; set; }

    /// <summary>Name of the map image used for this run (filename only).</summary>
    public string MapName { get; set; }

    /// <summary>True if all 4 ArUco corner markers were detected in the camera frame.</summary>
    public bool MarkersDetected { get; set; }

    /// <summary>How many of the 4 expected markers were found (0–4).</summary>
    public int MarkerCount { get; set; }

    /// <summary>The detector pipeline status (Ok, NoMarkers, Empty).</summary>
    public string DetectStatus { get; set; }

    /// <summary>Number of figurine blobs detected in the diff.</summary>
    public int FigurineCount { get; set; }

    /// <summary>The lens distortion coefficient calculated by AutoTuner.</summary>
    public double LensDistortionK1 { get; set; }

    /// <summary>Number of raw blobs detected before distortion fix.</summary>
    public int BlobsWithoutDistortionFix { get; set; }

    /// <summary>Number of raw blobs detected after distortion fix.</summary>
    public int BlobsWithDistortionFix { get; set; }

    /// <summary>Per-figurine detection details.</summary>
    public FigurineInfo[] Figurines { get; set; } = Array.Empty<FigurineInfo>();

    /// <summary>Wall-clock milliseconds for the detection pipeline (excluding camera grab).</summary>
    public double ProcessingMs { get; set; }

    /// <summary>Any error message if the pipeline failed.</summary>
    public string ErrorMessage { get; set; }

    /// <summary>Relative path to the saved raw camera frame image, if written.</summary>
    public string RawFramePath { get; set; }

    /// <summary>Relative path to the saved reference scene image, if written.</summary>
    public string ReferenceScenePath { get; set; }

    /// <summary>Relative path to the saved annotated result image, if written.</summary>
    public string AnnotatedFramePath { get; set; }

    /// <summary>Random seed used for the map crop (for reproducibility).</summary>
    public int RandomSeed { get; set; }

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    public class FigurineInfo
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float Radius { get; set; }
    }
}
