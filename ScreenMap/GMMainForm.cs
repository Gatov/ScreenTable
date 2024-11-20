﻿using ScreenMap.Logic;

namespace ScreenMap
{
    public partial class GMMainForm : DevExpress.XtraEditors.XtraForm
    {
        enum Mode { Normal, Calibrate };
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
            //controller.OnMessage += gmMapView1.
        }

        private void barCheckItem_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            _currentMode = (_currentMode == Mode.Normal) ? Mode.Calibrate : Mode.Normal;

            gmMapView1.CalibrationMode(_currentMode == Mode.Calibrate);
            barCheckItem.Checked = _currentMode == Mode.Calibrate;
        }
    }
}