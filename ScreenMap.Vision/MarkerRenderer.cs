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
    // Baseline marker geometry, calibrated at the detector reference height (540 px). The
    // rendered size scales up from here on bigger live screens (see DrawCornerFiducials) so
    // the camera gets enough pixels to decode the marker.
    public const int FiducialSizePx = 80;
    public const int FiducialQuietPx = 12;  // white quiet zone around the marker
    public const int FiducialRingPx = 8;    // black ring around the quiet zone

    // Reference height the baseline size is calibrated for, and the cap on how big the marker
    // may grow (2x baseline). The marker scales linearly with the rendered view's short side
    // between these, so a 1080p+ screen gets a markedly larger, easier-to-decode fiducial.
    private const int BaselineShortSidePx = 540;
    private const int MaxFiducialSizePx = FiducialSizePx * 2;
    // DICT_4X4_50 with a 1-bit border = 6 modules per side. The rendered size must be a
    // multiple of this so every module is an integer number of pixels; an uneven size gives
    // ragged module edges that the detector fails to decode off a re-photographed screen.
    private const int ModulesPerSide = 6;
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

        // Grow the marker on bigger screens: a fixed 80 px is a tiny fraction of a 4K view,
        // so the camera can't resolve enough pixels to decode it. Scale with the short side,
        // capped at 2x, and floored at the baseline so small windows are unaffected.
        int size = Math.Clamp(
            (int)Math.Round((double)FiducialSizePx * Math.Min(w, h) / BaselineShortSidePx),
            FiducialSizePx, MaxFiducialSizePx);
        // Snap to a whole number of modules so the marker decodes cleanly (see ModulesPerSide).
        size = (int)Math.Round((double)size / ModulesPerSide) * ModulesPerSide;
        // Quiet zone and ring scale with the marker so the white border stays at least one
        // ArUco module wide (the detector needs it to find the marker boundary).
        double scale = (double)size / FiducialSizePx;
        int quiet = (int)Math.Round(FiducialQuietPx * scale);
        int ring = (int)Math.Round(FiducialRingPx * scale);

        int corner = size + 2 * (quiet + ring);
        if (w < corner * 2 || h < corner * 2) return;
        var markers = ArucoMarkers.GetMarkers(size);
        var prevInterp = g.InterpolationMode;
        var prevComp = g.CompositingMode;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.CompositingMode = CompositingMode.SourceCopy;

        // Each fiducial gets a black ring around a white quiet zone. The white zone alone
        // vanishes against bright map content (and a black marker border washes out under
        // glare); the black ring gives a brightness-independent boundary so the detector
        // can localize the quiet zone on any map.
        int white = size + 2 * quiet;
        int black = white + 2 * ring;
        int half = black / 2;

        // Marker CENTERS sit at a fixed FRACTION of the view, so they land on the same map
        // world-point regardless of render resolution (the filmed screen vs the lower-res
        // detector reference). The detector aligns by mapping these centers to each other, so
        // the centers must be resolution-independent. Marker SIZE may differ between the two
        // renders (it scales with resolution) — that's fine, the detector matches by ArUco id
        // and center, not by pixel size.
        int fx = Math.Clamp((int)Math.Round(FiducialInsetFrac * w), half, w - half);
        int fy = Math.Clamp((int)Math.Round(FiducialInsetFrac * h), half, h - half);

        void DrawCentered(int cx, int cy, Bitmap marker)
        {
            g.FillRectangle(Brushes.Black, cx - half, cy - half, black, black);
            g.FillRectangle(Brushes.White, cx - half + ring, cy - half + ring, white, white);
            g.DrawImage(marker, cx - size / 2, cy - size / 2, size, size);
        }
        DrawCentered(fx, fy, markers[0]);
        DrawCentered(w - fx, fy, markers[1]);
        DrawCentered(w - fx, h - fy, markers[2]);
        DrawCentered(fx, h - fy, markers[3]);

        g.InterpolationMode = prevInterp;
        g.CompositingMode = prevComp;
    }

    /// <summary>
    /// Calculates the exact pixel coordinates of the four corner fiducials on a rendered
    /// scene of the given size. This perfectly matches the layout drawn by DrawCornerFiducials.
    /// Returns id -> centroid for ids 0..3.
    /// </summary>
    public static System.Collections.Generic.Dictionary<int, OpenCvSharp.Point2f> GetFiducialCenters(SizeF clientSize)
    {
        int w = (int)clientSize.Width;
        int h = (int)clientSize.Height;

        int size = Math.Clamp(
            (int)Math.Round((double)FiducialSizePx * Math.Min(w, h) / BaselineShortSidePx),
            FiducialSizePx, MaxFiducialSizePx);
        size = (int)Math.Round((double)size / ModulesPerSide) * ModulesPerSide;
        double scale = (double)size / FiducialSizePx;
        int quiet = (int)Math.Round(FiducialQuietPx * scale);
        int ring = (int)Math.Round(FiducialRingPx * scale);

        int black = size + 2 * quiet + 2 * ring;
        int half = black / 2;

        int fx = Math.Clamp((int)Math.Round(FiducialInsetFrac * w), half, w - half);
        int fy = Math.Clamp((int)Math.Round(FiducialInsetFrac * h), half, h - half);

        return new System.Collections.Generic.Dictionary<int, OpenCvSharp.Point2f>
        {
            { 0, new OpenCvSharp.Point2f(fx, fy) },
            { 1, new OpenCvSharp.Point2f(w - fx, fy) },
            { 2, new OpenCvSharp.Point2f(w - fx, h - fy) },
            { 3, new OpenCvSharp.Point2f(fx, h - fy) }
        };
    }
}
