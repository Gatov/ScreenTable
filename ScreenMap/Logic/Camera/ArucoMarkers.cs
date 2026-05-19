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
