using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace ScreenMap.Logic;

public static class FogUtil
{
    public static TextureBrush CreateSemitransparentBrushFromImage(Image original, float transparency)
    {
        var texture = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        var colorMatrix = new ColorMatrix { Matrix33 = transparency };
       
        ImageAttributes imageAttributes = new ImageAttributes();
        imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        using (Graphics g = Graphics.FromImage(texture))
        {
            var rect = new Rectangle(0, 0, original.Width, original.Height);
            g.DrawImage(original, rect, 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, imageAttributes);
        }
        // create and return a textured brush from the texture
        return new TextureBrush(texture);
    }
    public static double CalculateDistance(PointF p1, PointF p2)
    {
        float dx = p2.X - p1.X;
        float dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static RectangleF RectByCenter(this PointF center, int size)
    {
        return new RectangleF(center.X - size / 2F, center.Y - size / 2F, size, size);
    }

    public static float Distance(this PointF point1, PointF point2)
    {
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }
}