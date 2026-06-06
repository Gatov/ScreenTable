using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ScreenMap.Vision;

/// <summary>
/// Renders the four ArUco corner fiducials onto any Graphics surface.
/// Extracted from PlayersMap so the test harness can produce the same marker layout
/// without depending on the full ScreenMap application.
/// </summary>
public static class MarkerRenderer
{
    public const int FiducialSizePx = 80;
    public const int FiducialQuietPx = 12;  // white quiet zone around the marker
    public const int FiducialRingPx = 8;    // black ring around the quiet zone
    // Marker-center inset as a fraction of the view. Must be large enough that the black
    // square stays on-screen at the detector reference size (960x540) without clamping —
    // otherwise the fraction differs between the filmed screen and the reference and the
    // map misaligns. 0.12*540 = 64.8 > half the black square (60).
    public const float FiducialInsetFrac = 0.12f;

    /// <summary>
    /// Draws the four ArUco corner fiducials onto the given Graphics surface at the
    /// specified client size. This exactly mirrors the layout used by PlayersMap so the
    /// detector can align the camera frame against the rendered reference.
    /// </summary>
    public static void DrawCornerFiducials(Graphics g, SizeF clientSize)
    {
        int w = (int)clientSize.Width;
        int h = (int)clientSize.Height;
        int corner = FiducialSizePx + 2 * (FiducialQuietPx + FiducialRingPx);
        if (w < corner * 2 || h < corner * 2) return;
        var markers = ArucoMarkers.GetMarkers(FiducialSizePx);
        var prevInterp = g.InterpolationMode;
        var prevComp = g.CompositingMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;

        // Each fiducial gets a black ring around a white quiet zone. The white zone alone
        // vanishes against bright map content (and a black marker border washes out under
        // glare); the black ring gives a brightness-independent boundary so the detector
        // can localize the quiet zone on any map.
        const int quiet = FiducialQuietPx;
        const int ring = FiducialRingPx;
        int white = FiducialSizePx + 2 * quiet;
        int black = white + 2 * ring;
        int half = black / 2;

        // Marker CENTERS sit at a fixed FRACTION of the view, so they land on the same map
        // world-point regardless of render resolution (the filmed screen vs the lower-res
        // detector reference). The detector aligns by mapping these centers to each other,
        // so they must be resolution-independent. Markers stay a fixed pixel size (the
        // camera needs the pixels to decode them); only their position is fractional.
        int fx = Math.Clamp((int)Math.Round(FiducialInsetFrac * w), half, w - half);
        int fy = Math.Clamp((int)Math.Round(FiducialInsetFrac * h), half, h - half);

        void DrawCentered(int cx, int cy, Bitmap marker)
        {
            g.FillRectangle(Brushes.Black, cx - half, cy - half, black, black);
            g.FillRectangle(Brushes.White, cx - half + ring, cy - half + ring, white, white);
            g.DrawImage(marker, cx - FiducialSizePx / 2, cy - FiducialSizePx / 2, FiducialSizePx, FiducialSizePx);
        }
        DrawCentered(fx, fy, markers[0]);
        DrawCentered(w - fx, fy, markers[1]);
        DrawCentered(w - fx, h - fy, markers[2]);
        DrawCentered(fx, h - fy, markers[3]);

        g.InterpolationMode = prevInterp;
        g.CompositingMode = prevComp;
    }
}
