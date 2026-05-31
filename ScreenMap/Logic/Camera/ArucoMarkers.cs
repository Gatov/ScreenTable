using System.Drawing;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;

namespace ScreenMap.Logic.Camera;

/// <summary>
/// Generates and caches four corner ArUco fiducial bitmaps (DICT_4X4_50, IDs 0..3)
/// used to localize the screen in a camera frame.
///
/// Order: index 0 = top-left, 1 = top-right, 2 = bottom-right, 3 = bottom-left.
/// </summary>
public static class ArucoMarkers
{
    public const PredefinedDictionaryName DictName = PredefinedDictionaryName.Dict4X4_50;
    public const int MarkerCount = 4;

    public static readonly int[] CornerIds = { 0, 1, 2, 3 };

    /// <summary>
    /// Detector parameters tuned for fiducials filmed off a screen: the markers are a
    /// small fraction of a high-resolution frame, so the default MinMarkerPerimeterRate
    /// (0.03, relative to image size) would reject them. A wider adaptive-threshold range
    /// and sub-pixel corner refinement help with screen glare and soft edges.
    /// </summary>
    public static DetectorParameters CreateDetectorParameters() => new()
    {
        AdaptiveThreshWinSizeMin = 3,
        // Wide window range: in a 4K frame the fiducials are large, and a steeply angled
        // corner marker needs a bigger adaptive-threshold window to binarize cleanly.
        AdaptiveThreshWinSizeMax = 53,
        AdaptiveThreshWinSizeStep = 4,
        MinMarkerPerimeterRate = 0.01,
        MaxMarkerPerimeterRate = 4.0,
        PerspectiveRemoveIgnoredMarginPerCell = 0.10,
        CornerRefinementMethod = CornerRefineMethod.Subpix,
    };

    private static readonly object _lock = new();
    private static Bitmap[] _cache;
    private static int _cachedSize;

    public static Bitmap[] GetMarkers(int sidePixels)
    {
        lock (_lock)
        {
            if (_cache != null && _cachedSize == sidePixels) return _cache;
            DisposeCache();
            var bitmaps = new Bitmap[MarkerCount];
            using var dict = CvAruco.GetPredefinedDictionary(DictName);
            for (int i = 0; i < MarkerCount; i++)
            {
                using var mat = new Mat();
                dict.GenerateImageMarker(CornerIds[i], sidePixels, mat, borderBits: 1);
                bitmaps[i] = BitmapConverter.ToBitmap(mat);
            }
            _cache = bitmaps;
            _cachedSize = sidePixels;
            return _cache;
        }
    }

    private static void DisposeCache()
    {
        if (_cache == null) return;
        foreach (var bmp in _cache) bmp?.Dispose();
        _cache = null;
    }
}
