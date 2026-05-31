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
    private readonly DetectorParameters _params = ArucoMarkers.CreateDetectorParameters();
    private volatile bool _saveRequested;
    private const int DisplayMaxWidth = 1280;

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
        _picture.Click += (_, _) => _saveRequested = true;
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
            // Diagnostic: save the RAW frame (pre-overlay) when requested, so the actual
            // pixels the detector sees can be inspected.
            if (_saveRequested)
            {
                _saveRequested = false;
                SaveDiagnostic(frame);
            }

            // Detection runs on the full-resolution frame (markers need the pixels).
            CvAruco.DetectMarkers(frame, _dict, out var corners, out var ids, _params, out var rejected);
            int found = ids?.Length ?? 0;
            int rej = rejected?.Length ?? 0;
            if (found > 0)
                CvAruco.DrawDetectedMarkers(frame, corners, ids);

            // Downscale for display only — a 4K Mat->Bitmap every tick is too heavy.
            Mat display = frame;
            Mat scaled = null;
            if (frame.Width > DisplayMaxWidth)
            {
                int dh = frame.Height * DisplayMaxWidth / frame.Width;
                scaled = new Mat();
                Cv2.Resize(frame, scaled, new OpenCvSharp.Size(DisplayMaxWidth, dh), 0, 0, InterpolationFlags.Area);
                display = scaled;
            }

            var color = found >= ArucoMarkers.MarkerCount ? Scalar.LightGreen : Scalar.Orange;
            Cv2.PutText(display, $"markers: {found}/{ArucoMarkers.MarkerCount}  rejected: {rej}  {frame.Width}x{frame.Height}",
                new OpenCvSharp.Point(12, 28), HersheyFonts.HersheySimplex, 0.7, color, 2);
            Cv2.PutText(display, "click image to save a diagnostic frame",
                new OpenCvSharp.Point(12, display.Height - 14), HersheyFonts.HersheySimplex, 0.5, Scalar.Gray, 1);

            bitmap = BitmapConverter.ToBitmap(display);
            scaled?.Dispose();
        }

        var previous = _picture.Image;
        _picture.Image = bitmap;
        previous?.Dispose();
    }

    private void SaveDiagnostic(Mat raw)
    {
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = System.IO.Path.Combine(dir, $"aruco-frame-{stamp}.png");
            Cv2.ImWrite(path, raw);
            Text = $"Camera Preview — saved {path}";
        }
        catch (Exception ex)
        {
            Text = "Camera Preview — save failed: " + ex.Message;
        }
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
