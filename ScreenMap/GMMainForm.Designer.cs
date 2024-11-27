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
            DevExpress.Utils.SuperToolTip superToolTip1 = new DevExpress.Utils.SuperToolTip();
            DevExpress.Utils.ToolTipTitleItem toolTipTitleItem1 = new DevExpress.Utils.ToolTipTitleItem();
            DevExpress.Utils.ToolTipItem toolTipItem1 = new DevExpress.Utils.ToolTipItem();
            DevExpress.Utils.SuperToolTip superToolTip2 = new DevExpress.Utils.SuperToolTip();
            DevExpress.Utils.ToolTipTitleItem toolTipTitleItem2 = new DevExpress.Utils.ToolTipTitleItem();
            DevExpress.Utils.ToolTipItem toolTipItem2 = new DevExpress.Utils.ToolTipItem();
            DevExpress.Utils.SuperToolTip superToolTip3 = new DevExpress.Utils.SuperToolTip();
            DevExpress.Utils.ToolTipTitleItem toolTipTitleItem3 = new DevExpress.Utils.ToolTipTitleItem();
            DevExpress.Utils.ToolTipItem toolTipItem3 = new DevExpress.Utils.ToolTipItem();
            DevExpress.Utils.SuperToolTip superToolTip4 = new DevExpress.Utils.SuperToolTip();
            DevExpress.Utils.ToolTipTitleItem toolTipTitleItem4 = new DevExpress.Utils.ToolTipTitleItem();
            DevExpress.Utils.ToolTipItem toolTipItem4 = new DevExpress.Utils.ToolTipItem();
            gmMapView1 = new GmMapView();
            barManager1 = new DevExpress.XtraBars.BarManager(components);
            bar1 = new DevExpress.XtraBars.Bar();
            barButtonItemSave = new DevExpress.XtraBars.BarButtonItem();
            barCheckItemCalibrate = new DevExpress.XtraBars.BarCheckItem();
            barCheckItemMarks = new DevExpress.XtraBars.BarCheckItem();
            barListItemBrushes = new DevExpress.XtraBars.BarListItem();
            bar3 = new DevExpress.XtraBars.Bar();
            barStaticItemHint = new DevExpress.XtraBars.BarStaticItem();
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
            gmMapView1.Size = new System.Drawing.Size(682, 1000);
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
            barManager1.Items.AddRange(new DevExpress.XtraBars.BarItem[] { barCheckItemCalibrate, barButtonItemSave, barListItemBrushes, barCheckItemMarks, barStaticItemHint });
            barManager1.MaxItemId = 7;
            barManager1.StatusBar = bar3;
            // 
            // bar1
            // 
            bar1.BarName = "Tools";
            bar1.DockCol = 0;
            bar1.DockRow = 0;
            bar1.DockStyle = DevExpress.XtraBars.BarDockStyle.Top;
            bar1.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(barButtonItemSave), new DevExpress.XtraBars.LinkPersistInfo(barCheckItemCalibrate, true), new DevExpress.XtraBars.LinkPersistInfo(barCheckItemMarks), new DevExpress.XtraBars.LinkPersistInfo(barListItemBrushes, true) });
            bar1.Text = "Tools";
            // 
            // barButtonItemSave
            // 
            barButtonItemSave.Caption = "Save";
            barButtonItemSave.Id = 2;
            barButtonItemSave.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("barButtonItemSave.ImageOptions.Image");
            barButtonItemSave.ImageOptions.LargeImage = (System.Drawing.Image)resources.GetObject("barButtonItemSave.ImageOptions.LargeImage");
            barButtonItemSave.Name = "barButtonItemSave";
            toolTipTitleItem1.Text = "Save";
            toolTipItem1.Text = "Save current state of the map\r\nincluding revealed area and markers";
            superToolTip1.Items.Add(toolTipTitleItem1);
            superToolTip1.Items.Add(toolTipItem1);
            barButtonItemSave.SuperTip = superToolTip1;
            barButtonItemSave.ItemClick += barButtonItemSave_ItemClick;
            // 
            // barCheckItemCalibrate
            // 
            barCheckItemCalibrate.Caption = "Calibrate";
            barCheckItemCalibrate.Id = 1;
            barCheckItemCalibrate.ImageOptions.Image = Properties.Resources.bordersall_32x32;
            barCheckItemCalibrate.Name = "barCheckItemCalibrate";
            toolTipTitleItem2.Text = "Calibration Tool";
            toolTipItem2.Text = "L-Button to mark the grid\r\nthen\r\nMouse Wheel to adjust cell size";
            superToolTip2.Items.Add(toolTipTitleItem2);
            superToolTip2.Items.Add(toolTipItem2);
            barCheckItemCalibrate.SuperTip = superToolTip2;
            barCheckItemCalibrate.CheckedChanged += BarCheckItemCalibrateCheckedChanged;
            // 
            // barCheckItemMarks
            // 
            barCheckItemMarks.Caption = "Marks";
            barCheckItemMarks.Id = 5;
            barCheckItemMarks.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("barCheckItemMarks.ImageOptions.Image");
            barCheckItemMarks.ImageOptions.LargeImage = (System.Drawing.Image)resources.GetObject("barCheckItemMarks.ImageOptions.LargeImage");
            barCheckItemMarks.Name = "barCheckItemMarks";
            toolTipTitleItem3.Text = "Marking tool";
            toolTipItem3.Text = "Mouse Wheel - cycle through colors\r\nL-Button - Set marker (brush size)\r\nCtrl-L-Button - Clear marker";
            superToolTip3.Items.Add(toolTipTitleItem3);
            superToolTip3.Items.Add(toolTipItem3);
            barCheckItemMarks.SuperTip = superToolTip3;
            barCheckItemMarks.CheckedChanged += barCheckItemMarks_CheckedChanged;
            // 
            // barListItemBrushes
            // 
            barListItemBrushes.Caption = "3";
            barListItemBrushes.Id = 3;
            barListItemBrushes.ImageOptions.Image = (System.Drawing.Image)resources.GetObject("barListItemBrushes.ImageOptions.Image");
            barListItemBrushes.Name = "barListItemBrushes";
            barListItemBrushes.PaintStyle = DevExpress.XtraBars.BarItemPaintStyle.CaptionGlyph;
            barListItemBrushes.Strings.AddRange(new object[] { "0.5", "1", "2", "3", "4", "5", "8", "12" });
            toolTipTitleItem4.Text = "Brush Size";
            toolTipItem4.Text = "Brush size in cells";
            superToolTip4.Items.Add(toolTipTitleItem4);
            superToolTip4.Items.Add(toolTipItem4);
            barListItemBrushes.SuperTip = superToolTip4;
            barListItemBrushes.ListItemClick += barListItemBrushes_ListItemClick;
            // 
            // bar3
            // 
            bar3.BarName = "Status bar";
            bar3.CanDockStyle = DevExpress.XtraBars.BarCanDockStyle.Bottom;
            bar3.DockCol = 0;
            bar3.DockRow = 0;
            bar3.DockStyle = DevExpress.XtraBars.BarDockStyle.Bottom;
            bar3.LinksPersistInfo.AddRange(new DevExpress.XtraBars.LinkPersistInfo[] { new DevExpress.XtraBars.LinkPersistInfo(barStaticItemHint) });
            bar3.OptionsBar.AllowQuickCustomization = false;
            bar3.OptionsBar.DrawDragBorder = false;
            bar3.OptionsBar.UseWholeRow = true;
            bar3.Text = "Status bar";
            // 
            // barStaticItemHint
            // 
            barStaticItemHint.Caption = "Drop the image file or json file of the map";
            barStaticItemHint.Id = 6;
            barStaticItemHint.Name = "barStaticItemHint";
            // 
            // barDockControlTop
            // 
            barDockControlTop.CausesValidation = false;
            barDockControlTop.Dock = System.Windows.Forms.DockStyle.Top;
            barDockControlTop.Location = new System.Drawing.Point(0, 0);
            barDockControlTop.Manager = barManager1;
            barDockControlTop.Size = new System.Drawing.Size(969, 40);
            // 
            // barDockControlBottom
            // 
            barDockControlBottom.CausesValidation = false;
            barDockControlBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
            barDockControlBottom.Location = new System.Drawing.Point(0, 418);
            barDockControlBottom.Manager = barManager1;
            barDockControlBottom.Size = new System.Drawing.Size(969, 22);
            // 
            // barDockControlLeft
            // 
            barDockControlLeft.CausesValidation = false;
            barDockControlLeft.Dock = System.Windows.Forms.DockStyle.Left;
            barDockControlLeft.Location = new System.Drawing.Point(0, 40);
            barDockControlLeft.Manager = barManager1;
            barDockControlLeft.Size = new System.Drawing.Size(0, 378);
            // 
            // barDockControlRight
            // 
            barDockControlRight.CausesValidation = false;
            barDockControlRight.Dock = System.Windows.Forms.DockStyle.Right;
            barDockControlRight.Location = new System.Drawing.Point(969, 40);
            barDockControlRight.Manager = barManager1;
            barDockControlRight.Size = new System.Drawing.Size(0, 378);
            // 
            // xtraScrollableControl1
            // 
            xtraScrollableControl1.Controls.Add(gmMapView1);
            xtraScrollableControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            xtraScrollableControl1.Location = new System.Drawing.Point(0, 40);
            xtraScrollableControl1.Name = "xtraScrollableControl1";
            xtraScrollableControl1.Size = new System.Drawing.Size(969, 378);
            xtraScrollableControl1.TabIndex = 5;
            // 
            // GMMainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(969, 440);
            Controls.Add(xtraScrollableControl1);
            Controls.Add(barDockControlLeft);
            Controls.Add(barDockControlRight);
            Controls.Add(barDockControlBottom);
            Controls.Add(barDockControlTop);
            IconOptions.Icon = (System.Drawing.Icon)resources.GetObject("GMMainForm.IconOptions.Icon");
            Name = "GMMainForm";
            Text = "GM View";
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
        private DevExpress.XtraBars.BarCheckItem barCheckItemCalibrate;
        private DevExpress.XtraBars.BarButtonItem barButtonItemSave;
        private ScrollableContainer xtraScrollableControl1;
        private DevExpress.XtraBars.BarListItem barListItemBrushes;
        private DevExpress.XtraBars.BarCheckItem barCheckItemMarks;
        private DevExpress.XtraBars.BarStaticItem barStaticItemHint;
    }
}