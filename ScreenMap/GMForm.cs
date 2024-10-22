using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.LookAndFeel;
using DevExpress.Utils;
using DevExpress.Utils.Drawing;

namespace ScreenMap
{
    public partial class GMForm : DevExpress.XtraEditors.DirectXForm
    {
        public GMForm()
        {
            InitializeComponent();
            GmMapView view = new GmMapView();
            view.Dock = DockStyle.Fill;
            directXFormContainerControl1.Controls.Add(view);
            PaintEx += OnPaintEx; 
        }

        private void OnPaintEx(object sender, XtraPaintEventArgs e)
        {
            e.Cache.DrawRectangle(Pens.Blue, new Rectangle(10,10,200,200));
        }
    }
}
