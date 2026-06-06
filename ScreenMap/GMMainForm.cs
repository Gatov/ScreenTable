using System;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using ScreenMap.Forms;
using ScreenMap.Logic;
using ScreenMap.Vision;
using ScreenMap.Logic.Tools;
using ScreenTable.Tools;

namespace ScreenMap
{
    public partial class GMMainForm : DevExpress.XtraEditors.XtraForm
    {
        enum Mode { Normal, Calibrate, Mark };
        Mode _currentMode;
        private ScreenMapWebServer _webServer;
        private CloudflareTunnel _tunnel;
        private CameraSettings _cameraSettings;
        private DetectionStore _detectionStore;
        private DetectionService _detectionService;
        private CameraPreviewForm _previewForm;
        private PlayerController _controller;
        private static readonly Size CameraSnapshotSize = new Size(960, 540);

        public GMMainForm()
        {
            InitializeComponent();
            // Add scrollbars tho the form so user can scroll to see all controls
            PlayerController controller = new PlayerController();
            _controller = controller;
            var playerView = new PlayersForm(controller);

            playerView.Show();
            gmMapView1.Map.OnMessage += controller.ProcessMessage;
            controller.OnMessage += gmMapView1.Map.ProcessMessage;
            gmMapView1.ToolChange += GmMapViewOnToolChange;
            //controller.OnMessage += gmMapView1.
            xtraScrollableControl1.AlwaysScrollActiveControlIntoView = false;

            _cameraSettings = CameraSettings.Load();
            _detectionStore = new DetectionStore();
            // Player screen never shows detections (it is filmed). The web snapshot does.
            controller.SetDetectionOverlay(_detectionStore, () => _cameraSettings.ShowFigurines);
            gmMapView1.SetDetectionOverlay(_detectionStore,
                () => _cameraSettings.ShowOnGmView, () => _cameraSettings.ShowFigurines);

            _webServer = new ScreenMapWebServer(size =>
            {
                var cached = controller.TryGetCachedSnapshotPng(size);
                if (cached != null) return cached;
                return InvokeRequired
                    ? (byte[])Invoke(() => controller.RenderSnapshotPng(size))
                    : controller.RenderSnapshotPng(size);
            });
            try
            {
                _webServer.Start();
            }
            catch (HttpListenerException ex) when (ex.ErrorCode == 5)
            {
                MessageBox.Show(
                    "Web server could not start (Access Denied).\n" +
                    "Run this once as Administrator to register the URL ACL:\n" +
                    "  netsh http add urlacl url=http://+:5001/ user=Everyone\n" +
                    "After that, the app can run as a normal user.",
                    "ScreenMap Web Server", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            FormClosed += (_, _) =>
            {
                _previewForm?.Dispose();
                _detectionService?.Dispose();
                _webServer.Dispose();
                _tunnel?.Dispose();
            };

            InitializeDetectionService();
            barEditItemMinis.EditValue = _cameraSettings.ExpectedFigurines;
        }

        private void InitializeDetectionService()
        {
            _detectionService?.Dispose();
            _detectionService = new DetectionService(
                _detectionStore,
                // Detector diffs against a CLEAN map — never include the detection overlay,
                // or its circles become diffs and feed back as more false detections.
                size => SafeInvoke(() => _controller.RenderSnapshotBitmap(size, includeDetections: false)),
                size => SafeInvoke(() => _controller.GetSnapshotViewRect(size)),
                CameraSnapshotSize,
                size => SafeInvoke(() => _controller.GetPixelsPerCell(size)));
            _detectionService.DetectionsUpdated += OnDetectionsUpdated;
            _detectionService.Apply(_cameraSettings);
            UpdateCameraStatus();
        }

        private T SafeInvoke<T>(Func<T> fn)
        {
            if (IsDisposed || !IsHandleCreated) return default;
            try { return (T)Invoke(fn); }
            catch (ObjectDisposedException) { return default; }
            catch (InvalidOperationException) { return default; }
        }

        private void OnDetectionsUpdated()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new Action(OnDetectionsUpdated)); return; }
            _controller.InvalidateSnapshot();
            gmMapView1.InvalidateOverlay();
            UpdateCameraStatus();
        }

        private void UpdateCameraStatus()
        {
            var s = _detectionStore.Status;
            var text = _detectionStore.StatusText;
            // TEMP DIAGNOSTIC: show per-axis pixels-per-cell for the detection snapshot.
            var (ppx, ppy) = _controller.GetPixelsPerCellXY(CameraSnapshotSize);
            barStaticItemCameraStatus.Caption = $"Camera: {text} | ppcXY={ppx:0}/{ppy:0}";
        }

        private void barEditItemMinis_EditValueChanged(object sender, EventArgs e)
        {
            _cameraSettings.ExpectedFigurines = Convert.ToInt32(barEditItemMinis.EditValue ?? 0);
            _cameraSettings.Save();
            _detectionService.Apply(_cameraSettings);
        }

        private void barButtonItemCamera_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            // Release our camera handle first: the dialog probes device indices by
            // opening each one, and DSHOW is exclusive — a held device would fail to
            // probe and vanish from the list (locking the camera onto ourselves).
            _detectionService.Apply(new CameraSettings { Enabled = false });
            try
            {
                using var dlg = new CameraSettingsForm(_cameraSettings);
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _cameraSettings = dlg.Result;
            }
            finally
            {
                _detectionService.Apply(_cameraSettings);
                UpdateCameraStatus();
                gmMapView1.InvalidateOverlay();
                _controller.InvalidateSnapshot();
            }
        }

        private void barButtonItemPreview_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_previewForm != null && !_previewForm.IsDisposed)
            {
                _previewForm.Activate();
                return;
            }
            _previewForm = new CameraPreviewForm(_detectionService);
            _previewForm.FormClosed += (_, _) => _previewForm = null;
            _previewForm.Show(this);
        }

        private void GmMapViewOnToolChange(ITool obj)
        {
            barCheckItemCalibrate.Checked = obj is ToolCalibrate;
            barCheckItemMarks.Checked = obj is MarkingTool;
            switch (obj)
            {
                case ToolCalibrate: _currentMode = Mode.Calibrate; break;
                case MarkingTool: _currentMode = Mode.Mark; break;
                default: _currentMode = Mode.Normal; break;
            }
            barStaticItemHint.Caption = obj.Hint;
        }

        private void BarCheckItemCalibrateCheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_currentMode == Mode.Normal)
                _currentMode = Mode.Calibrate;
            else if (_currentMode == Mode.Calibrate)
                _currentMode = Mode.Normal;
            else
            {
                barCheckItemCalibrate.Checked = false;
                return;
            }
            gmMapView1.CalibrationMode(_currentMode == Mode.Calibrate);
            barCheckItemCalibrate.Checked = _currentMode == Mode.Calibrate;
        }

        private void barButtonItemSave_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            gmMapView1.SaveFile();
        }

        private void barListItemBrushes_ListItemClick(object sender, DevExpress.XtraBars.ListItemClickEventArgs e)
        {
            var str = barListItemBrushes.Strings[e.Index];
            gmMapView1.SetBrushSize(float.Parse(str));
            barListItemBrushes.Caption = str;
        }

        private void barCheckItemGrid_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            gmMapView1.SetGridVisible(barCheckItemGrid.Checked);
        }

        private async void barButtonItemShare_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_tunnel != null && _tunnel.IsRunning)
            {
                Clipboard.SetText(_tunnel.Url);
                MessageBox.Show(
                    $"Cloudflare tunnel is already running.\n\n{_tunnel.Url}\n\n(copied to clipboard)",
                    "Share", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            barButtonItemShare.Enabled = false;
            var tunnel = new CloudflareTunnel();
            try
            {
                var url = await tunnel.StartAsync(ScreenMapWebServer.Port, TimeSpan.FromSeconds(20));
                _tunnel = tunnel;
                Clipboard.SetText(url);
                barButtonItemShare.Caption = "Shared";
                MessageBox.Show(
                    $"Tunnel is live:\n\n{url}\n\n(copied to clipboard)",
                    "Share", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                tunnel.Dispose();
                MessageBox.Show(
                    $"Could not start cloudflared tunnel.\n\n{ex.Message}",
                    "Share", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                barButtonItemShare.Enabled = true;
            }
        }

        private void barCheckItemMarks_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_currentMode == Mode.Normal)
                _currentMode = Mode.Mark;
            else if (_currentMode == Mode.Mark)
                _currentMode = Mode.Normal;
            else
            {
                barCheckItemMarks.Checked = false;
                return;
            }

            gmMapView1.MarkingMode(_currentMode == Mode.Mark);
            barCheckItemMarks.Checked = _currentMode == Mode.Mark;

        }
    }
}