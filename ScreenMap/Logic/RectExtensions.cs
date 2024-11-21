using System.Drawing;

namespace ScreenMap.Logic;

public static class RectExtensions
{
    public static RectangleF Scale(this RectangleF rect, float scale)
    {
        return new RectangleF(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
    }
    public static RectangleF Translate(this RectangleF rect, float x, float y)
    {
        return rect with { X = rect.X +x, Y = rect.Y +y };
    }
    public static RectangleF Scale(this RectangleF rect, float scaleX,float scaleY)
    {
        return new RectangleF(rect.X * scaleX, rect.Y * scaleY, rect.Width * scaleX, rect.Height * scaleY);
    }
    public static RectangleF Scale(this Rectangle rect, float scale)
    {
        return new RectangleF(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
    }
}