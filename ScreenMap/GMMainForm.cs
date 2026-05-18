using System.Drawing;
using System.Net;
using System.Windows.Forms;
using ScreenMap.Logic;
using ScreenMap.Logic.Tools;
using ScreenTable.Tools;

namespace ScreenMap
{
    public partial class GMMainForm : DevExpress.XtraEditors.XtraForm
    {
        enum Mode { Normal, Calibrate, Mark };
        Mode _currentMode;
        private ScreenMapWebServer _webServer;

        public GMMainForm()
        {
            InitializeComponent();
            // Add scrollbars tho the form so user can scroll to see all controls
            PlayerController controller = new PlayerController();
            var playerView = new PlayersForm(controller);

            playerView.Show();
            gmMapView1.Map.OnMessage += controller.ProcessMessage;
            controller.OnMessage += gmMapView1.Map.ProcessMessage;
            gmMapView1.ToolChange += GmMapViewOnToolChange;
            //controller.OnMessage += gmMapView1.
            xtraScrollableControl1.AlwaysScrollActiveControlIntoView = false;

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
                    "Run as Administrator to enable http://localhost/ScreenMapView.",
                    "ScreenMap Web Server", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            FormClosed += (_, _) => _webServer.Dispose();
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