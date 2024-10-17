using System.Drawing.Imaging;

namespace ScreenTable;

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
}