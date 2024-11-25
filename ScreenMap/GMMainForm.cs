using ScreenMap.Logic;

namespace ScreenMap
{
    public partial class GMMainForm : DevExpress.XtraEditors.XtraForm
    {
        enum Mode { Normal, Calibrate, Mark };
        Mode _currentMode;
        private readonly PlayersForm _playerView;

        public GMMainForm()
        {
            InitializeComponent();
            // Add scrollbars tho the form so user can scroll to see all controls
            PlayerController controller = new PlayerController();
            _playerView = new PlayersForm(controller);

            _playerView.Show();
            gmMapView1.Map.OnMessage += controller.ProcessMessage;
            controller.OnMessage += gmMapView1.Map.ProcessMessage;
            //controller.OnMessage += gmMapView1.
        }

        private void barCheckItem_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_currentMode == Mode.Normal)
                _currentMode = Mode.Calibrate;
            else if (_currentMode == Mode.Calibrate)
                _currentMode = Mode.Normal;
            else return;
            gmMapView1.CalibrationMode(_currentMode == Mode.Calibrate);
            barCheckItem.Checked = _currentMode == Mode.Calibrate;
        }

        private void barButtonItemSave_ItemClick(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            gmMapView1.SaveFile();
        }

        private void barListItemBrushes_ListItemClick(object sender, DevExpress.XtraBars.ListItemClickEventArgs e)
        {
            var str = barListItemBrushes.Strings[e.Index];
            gmMapView1.SetBrushSize(float.Parse(str));
        }

        private void barCheckItemMarks_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            if (_currentMode == Mode.Normal)
                _currentMode = Mode.Mark;
            else if (_currentMode == Mode.Mark)
                _currentMode = Mode.Normal;
            else return;

            gmMapView1.MarkingMode(_currentMode == Mode.Mark);
            barCheckItemMarks.Checked = _currentMode == Mode.Mark;

        }
    }
}