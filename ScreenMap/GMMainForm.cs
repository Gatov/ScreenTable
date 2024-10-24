using DevExpress.LookAndFeel;
using DevExpress.Utils.Drawing;
using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScreenMap
{
    public partial class GMMainForm : DevExpress.XtraEditors.XtraForm
    {
        enum Mode { Normal, Calibrate };
        Mode _currentMode;
        public GMMainForm()
        {
            InitializeComponent();
        }

        private void barCheckItem_CheckedChanged(object sender, DevExpress.XtraBars.ItemClickEventArgs e)
        {
            _currentMode = (_currentMode == Mode.Normal) ? Mode.Calibrate : Mode.Normal;

            gmMapView1.CalibrationMode(_currentMode == Mode.Calibrate);
            barCheckItem.Checked = _currentMode == Mode.Calibrate;
        }
    }
}