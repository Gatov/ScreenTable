using System.Drawing;

namespace ScreenMap.Logic.Camera;

public readonly struct FigurineDetection
{
    public PointF Center { get; }
    public float Radius { get; }

    public FigurineDetection(PointF center, float radius)
    {
        Center = center;
        Radius = radius;
    }
}
