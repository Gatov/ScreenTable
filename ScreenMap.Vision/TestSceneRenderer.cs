using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ScreenMap.Vision;

/// <summary>
/// Renders a complete test scene: a map background (or solid color) with the four
/// corner ArUco fiducials. This is the image the test harness displays on screen and
/// the reference image the detector diffs the warped camera frame against.
/// </summary>
public static class TestSceneRenderer
{
    /// <summary>
    /// Renders a test scene at the given size. If <paramref name="backgroundMap"/> is
    /// provided, a random crop of it is drawn into the scene (simulating a panned/zoomed
    /// view); otherwise a dark solid background is used.
    /// </summary>
    /// <param name="size">Output bitmap dimensions.</param>
    /// <param name="backgroundMap">Optional map image to crop and draw as background.</param>
    /// <param name="rng">Random source for pan/zoom. If null, a new Random is created.</param>
    public static Bitmap RenderScene(Size size, Bitmap backgroundMap = null, Random rng = null)
    {
        var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        if (backgroundMap != null)
        {
            DrawRandomCrop(g, size, backgroundMap, rng ?? new Random());
        }
        else
        {
            g.Clear(Color.FromArgb(60, 60, 60));
        }

        MarkerRenderer.DrawCornerFiducials(g, size);
        return bmp;
    }

    /// <summary>
    /// Draws a random crop of the source map into the output, simulating a
    /// panned and zoomed player view.
    /// </summary>
    private static void DrawRandomCrop(Graphics g, Size outputSize, Bitmap map, Random rng)
    {
        // Choose a zoom factor: show between 30% and 80% of the map's smallest dimension.
        float zoomFrac = 0.3f + (float)rng.NextDouble() * 0.5f;

        // Calculate the crop size in map pixels that would fill the output.
        float aspectRatio = (float)outputSize.Width / outputSize.Height;
        float cropH = map.Height * zoomFrac;
        float cropW = cropH * aspectRatio;

        // If the crop exceeds the map width, constrain by width instead.
        if (cropW > map.Width * 0.95f)
        {
            cropW = map.Width * zoomFrac;
            cropH = cropW / aspectRatio;
        }

        // Random position within the map.
        float maxX = Math.Max(0, map.Width - cropW);
        float maxY = Math.Max(0, map.Height - cropH);
        float x = (float)rng.NextDouble() * maxX;
        float y = (float)rng.NextDouble() * maxY;

        var srcRect = new RectangleF(x, y, cropW, cropH);
        var dstRect = new RectangleF(0, 0, outputSize.Width, outputSize.Height);

        g.DrawImage(map, dstRect, srcRect, GraphicsUnit.Pixel);
    }
}
