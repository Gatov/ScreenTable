using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using ScreenMap.Logic.Camera;

namespace ScreenMap.Forms;

/// <summary>
/// Live, non-modal preview of the raw camera feed with the detected ArUco corner
/// fiducials drawn on top, so the user can aim the camera until all four are visible.
/// The camera is owned by <see cref="DetectionService"/>; this form only pulls the
/// latest grabbed frame and keeps the device open via SetPreviewActive while shown.
/// </summary>
public sealed class CameraPreviewForm : Form
{
    private readonly DetectionService _service;
    private readonly PictureBox _picture;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly Dictionary _dict = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
    private readonly DetectorParameters _params = new();

    public CameraPreviewForm(DetectionService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Text = "Camera Preview";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new System.Drawing.Size(800, 480);
        MinimumSize = new System.Drawing.Size(320, 240);

        _picture = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black
        };
        Controls.Add(_picture);

        _uiTimer = new System.Windows.Forms.Timer { Interval = 66 }; // ~15 fps
        _uiTimer.Tick += OnUiTick;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _service.SetPreviewActive(true);
        _uiTimer.Start();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _uiTimer.Stop();
        _service.SetPreviewActive(false);
        base.OnFormClosing(e);
    }

    private void OnUiTick(object sender, EventArgs e)
    {
        if (!_service.TryGetLatestFrame(out var frame)) return;

        Bitmap bitmap;
        using (frame)
        {
            CvAruco.DetectMarkers(frame, _dict, out var corners, out var ids, _params, out _);
            int found = ids?.Length ?? 0;
            if (found > 0)
                CvAruco.DrawDetectedMarkers(frame, corners, ids);

            var color = found >= ArucoMarkers.MarkerCount ? Scalar.LightGreen : Scalar.Orange;
            Cv2.PutText(frame, $"markers: {found}/{ArucoMarkers.MarkerCount}",
                new OpenCvSharp.Point(12, 32), HersheyFonts.HersheySimplex, 0.9, color, 2);

            bitmap = BitmapConverter.ToBitmap(frame);
        }

        var previous = _picture.Image;
        _picture.Image = bitmap;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _uiTimer?.Dispose();
            _dict?.Dispose();
            _picture?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
