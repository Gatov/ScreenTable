using System.Drawing;

namespace ScreenMap.Vision;

public readonly struct FigurineDetection
{
    public PointF Center { get; }
    public float Radius { get; }
    /// <summary>Detection confidence: mean color distance (BGR difference-vector magnitude)
    /// inside the blob. Higher = more strongly differs from the map. Used to rank which blobs
    /// to keep against the expected count.</summary>
    public float Score { get; }

    public FigurineDetection(PointF center, float radius, float score = 0f)
    {
        Center = center;
        Radius = radius;
        Score = score;
    }
}
