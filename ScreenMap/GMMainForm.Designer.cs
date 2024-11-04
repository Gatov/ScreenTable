using ScreenMap.Controls;

namespace ScreenMap
{
    partial class GMMainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GMMainForm));
            gmMapView1 = new GmMapView();
            barManager1 = new DevExpress.XtraBars.BarManager(components);
            bar1 = new DevExpress.XtraBars.Bar();
            barCheckItem = new DevExpress.XtraBars.BarCheckItem();
            bar3 = new DevExpress.XtraBars.Bar();
            barDockControlTop = new DevExpress.XtraBars.BarDockControl();
            barDockControlBottom = new DevExpress.XtraBars.BarDockControl();
            barDockControlLeft = new DevExpress.XtraBars.BarDockControl();
            barDockControlRight = new DevExpress.XtraBars.BarDockControl();
            xtraScrollableControl1 = new ScrollableContainer();
            ((System.ComponentModel.ISupportInitialize)barManager1).BeginInit();
            xtraScrollableControl1.SuspendLayout();
            SuspendLayout();
            // 
            // gmMapView1
            // 
            gmMapView1.AllowDrop = true;
            gmMapView1.Appearance.BackColor = System.Drawing.SystemColors.Info;
            gmMapView1.Appearance.Options.UseBackColor = true;
            gmMapView1.Location = new System.Drawing.Point(0, 0);
            gmMapView1.Name = "gmMapView1";
            gmMapView1.Size = new System.Drawing.Size(933, 352);
            gmMapView1.TabIndex = 0;
            gmMapView1.ZoomLevel = 1F;
            // 
            // barManager1
            // 
            barManager1.Bars.AddRange(new DevExpress.XtraBars.Bar[] { bar1, bar3 });
            barManager1.DockControls.Add(barDockControlTop);
            barManager1.DockControls.Add(barDockControlBottom);
            barManager1.DockControls.Add(barDockControlLeft);
            barManager1.DockControls.Add(barDockControlRight);
            barManager1.Form = this;
            barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] { barCheckItem });
            barManager1.MaxItemId = 2;
            barManager1.StatusBar = bar3;
            // 
            // bar1
            // 
            bar1.BarName = "Tools";
            bar1.DockCol = 0;
            bar1.DockRow = 0;
            bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
            bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(barCheckItem) });
            bar1.Text = "Tools";
            // 
            // barCheckItem
            // 
            barCheckItem.Caption = "barCheckItem1";
            barCheckItem.Id = 1;
            barCheckItem.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("barCheckItem.ImageOptions.Image");
            barCheckItem.Name = "barCheckItem";
            barCheckItem.CheckedChanged += barCheckItem_CheckedChanged;
            // 
            // bar3
            // 
            bar3.BarName = "Status bar";
            bar3.CanDockStyle = DevExpress.XtraBars.BarCanDockStyle.Bottom;
            bar3.DockCol = 0;
            bar3.DockRow = 0;
            bar3.DockStyle = DevExpress.XtraBars.BarDockStyle.Bottom;
            bar3.OptionsBar.AllowQuickCustomization = false;
            bar3.OptionsBar.DrawDragBorder = false;
            bar3.OptionsBar.UseWholeRow = true;
            bar3.Text = "Status bar";
            // 
            // barDockControlTop
            // 
            barDockControlTop.CausesValidation = false;
            barDockControlTop.Dock = System.Windows.Forms.DockStyle.Top;
            barDockControlTop.Location = new System.Drawing.Point(0, 0);
            barDockControlTop.Manager = barManager1;
            barDockControlTop.Size = new System.Drawing.Size(978, 24);
            // 
            // barDockControlBottom
            // 
            barDockControlBottom.CausesValidation = false;
            barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            barDockControlBottom.Location = new System.Drawing.Point(0, 382);
            barDockControlBottom.Manager = barManager1;
            barDockControlBottom.Size = new System.Drawing.Size(978, 20);
            // 
            // barDockControlLeft
            // 
            barDockControlLeft.CausesValidation = false;
            barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            barDockControlLeft.Location = new System.Drawing.Point(0, 24);
            barDockControlLeft.Manager = barManager1;
            barDockControlLeft.Size = new System.Drawing.Size(0, 358);
            // 
            // barDockControlRight
            // 
            barDockControlRight.CausesValidation = false;
            barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
            barDockControlRight.Location = new System.Drawing.Point(978, 24);
            barDockControlRight.Manager = barManager1;
            barDockControlRight.Size = new System.Drawing.Size(0, 358);
            // 
            // xtraScrollableControl1
            // 
            xtraScrollableControl1.Controls.Add(gmMapView1);
            xtraScrollableControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            xtraScrollableControl1.Location = new System.Drawing.Point(0, 24);
            xtraScrollableControl1.Name = "xtraScrollableControl1";
            xtraScrollableControl1.Size = new System.Drawing.Size(978, 358);
            xtraScrollableControl1.TabIndex = 5;
            // 
            // GMMainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(978, 402);
            Controls.Add(xtraScrollableControl1);
            Controls.Add(barDockControlLeft);
            Controls.Add(barDockControlRight);
            Controls.Add(barDockControlBottom);
            Controls.Add(barDockControlTop);
            Name = "GMMainForm";
            Text = "XtraForm1";
            ((System.ComponentModel.ISupportInitialize)barManager1).EndInit();
            xtraScrollableControl1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private GmMapView gmMapView1;
        private DevExpress.XtraBars.BarManager barManager1;
        private DevExpress.XtraBars.Bar bar1;
        private DevExpress.XtraBars.Bar bar3;
        private DevExpress.XtraBars.BarDockControl barDockControlTop;
        private DevExpress.XtraBars.BarDockControl barDockControlBottom;
        private DevExpress.XtraBars.BarDockControl barDockControlLeft;
        private DevExpress.XtraBars.BarDockControl barDockControlRight;
        private DevExpress.XtraBars.BarCheckItem barCheckItem;
        private DevExpress.XtraEditors.XtraScrollableControl xtraScrollableControl1;
    }
}