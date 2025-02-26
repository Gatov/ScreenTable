using DevExpress.LookAndFeel;
using DevExpress.Skins;
using DevExpress.UserSkins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.Drawing;
using System.IO;

namespace ScreenMap
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            using(FileStream fs = new FileStream(@"C:\Temp\out.ico", FileMode.Create, FileAccess.Write))
                icon.Save(fs);
            WindowsFormsSettings.ForceDirectXPaint();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GMMainForm());
        }
    }
}
