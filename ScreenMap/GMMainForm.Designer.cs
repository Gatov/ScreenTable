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
            gmMapView1 = new GmMapView();
            SuspendLayout();
            // 
            // gmMapView1
            // 
            gmMapView1.AllowDrop = true;
            gmMapView1.Dock = System.Windows.Forms.DockStyle.Fill;
            gmMapView1.Location = new System.Drawing.Point(0, 0);
            gmMapView1.Name = "gmMapView1";
            gmMapView1.Size = new System.Drawing.Size(978, 402);
            gmMapView1.TabIndex = 0;
            // 
            // GMMainForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(978, 402);
            Controls.Add(gmMapView1);
            Name = "GMMainForm";
            Text = "XtraForm1";
            ResumeLayout(false);
        }

        #endregion

        private GmMapView gmMapView1;
    }
}