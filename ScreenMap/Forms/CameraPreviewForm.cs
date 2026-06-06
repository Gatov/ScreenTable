using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Aruco;
using OpenCvSharp.Extensions;
using ScreenMap.Vision;

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
    private readonly CheckBox _showCrops;
    private readonly Button _autoAdjust;
    private readonly System.Windows.Forms.Timer _uiTimer;
    private readonly Dictionary _dict = CvAruco.GetPredefinedDictionary(ArucoMarkers.DictName);
    private readonly DetectorParameters _params = ArucoMarkers.CreateDetectorParameters();
    private volatile bool _saveRequested;
    private const int DisplayMaxWidth = 1280;
    private const int CropTile = 160; // px per isolated-figurine thumbnail in the montage

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

        // Added after the Fill picture so it docks above it (WinForms docks in reverse z-order).
        _showCrops = new CheckBox
        {
            Dock = DockStyle.Top,
            Text = "Show isolated figurines",
            Height = 28,
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White
        };
        Controls.Add(_showCrops);

        // Docked above the checkbox (reverse z-order): place one minimal token, then find the
        // sensitivity/size that isolates it.
        _autoAdjust = new Button
        {
            Dock = DockStyle.Top,
            Text = "Auto-adjust detection",
            Height = 30,
            BackColor = Color.FromArgb(48, 48, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _autoAdjust.Click += OnAutoAdjustClick;
        Controls.Add(_autoAdjust);

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

        // When toggled on, replace the feed with a montage of the isolated figurine crops.
        if (_showCrops.Checked && _service.TryGetLatestCrops(out var crops))
        {
            var montage = BuildCropMontage(crops);
            foreach (var c in crops) c.Dispose();
            if (montage != null) { bitmap.Dispose(); bitmap = montage; }
        }

        var previous = _picture.Image;
        _picture.Image = bitmap;
        previous?.Dispose();
    }

    /// <summary>Lays the circular-masked crops out in a single horizontal strip of fixed-size
    /// tiles on a black canvas. Returns null when there is nothing to show.</summary>
    private static Bitmap BuildCropMontage(Mat[] crops)
    {
        if (crops == null || crops.Length == 0) return null;
        int n = crops.Length;
        var canvas = new Bitmap(CropTile * n, CropTile);
        using (var g = Graphics.FromImage(canvas))
        {
            g.Clear(Color.Black);
            for (int i = 0; i < n; i++)
            {
                if (crops[i].Empty()) continue;
                using var thumb = BitmapConverter.ToBitmap(crops[i]);
                g.DrawImage(thumb, new Rectangle(i * CropTile, 0, CropTile, CropTile));
            }
        }
        return canvas;
    }

    private void OnAutoAdjustClick(object sender, EventArgs e)
    {
        var prompt = MessageBox.Show(this,
            "Place ONE token (the smallest you use) on the map and clear everything else, " +
            "then click OK.\n\nAuto-adjust will find the sensitivity and minimum size that " +
            "isolate just that token.",
            "Auto-adjust detection", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
        if (prompt != DialogResult.OK) return;

        _autoAdjust.Enabled = false;
        Cursor = Cursors.WaitCursor;
        try
        {
            var result = _service.AutoTune();
            Text = result.Success
                ? "Camera Preview — auto-adjust: " + result.Message
                : "Camera Preview — auto-adjust failed: " + result.Message;
            if (!result.Success)
                MessageBox.Show(this, result.Message, "Auto-adjust",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            Cursor = Cursors.Default;
            _autoAdjust.Enabled = true;
        }
    }

    private void SaveDiagnostic(Mat raw)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ScreenMapCaptures");
            System.IO.Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            // Camera frame — the detector's input.
            var framePath = System.IO.Path.Combine(dir, $"frame-{stamp}.png");
            Cv2.ImWrite(framePath, raw);

            // Reference player view — the image the warped frame is diffed against.
            // Together the pair reproduces the full object-detection pipeline offline.
            string viewNote = "";
            using (var view = _service.RenderReferenceView())
            {
                if (view != null)
                {
                    var viewPath = System.IO.Path.Combine(dir, $"view-{stamp}.png");
                    view.Save(viewPath, System.Drawing.Imaging.ImageFormat.Png);
                    viewNote = " + view";
                }
                else
                {
                    viewNote = " (no reference view — no map?)";
                }
            }
            Text = $"Camera Preview — saved frame-{stamp}{viewNote} to {dir}";
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
