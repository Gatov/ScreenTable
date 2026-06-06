using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenMap.TestHarness;

/// <summary>
/// Borderless fullscreen form that displays the rendered test scene (map + markers)
/// on the specified screen. The form has no chrome and no controls — it just fills the
/// entire monitor with the scene bitmap so the overhead camera sees it.
/// </summary>
public sealed class DisplayForm : Form
{
    private Bitmap _scene;

    public DisplayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
    }

    /// <summary>
    /// Positions the form to fill the specified screen and makes it topmost.
    /// </summary>
    public void GoFullscreen(Screen screen)
    {
        Bounds = screen.Bounds;
        TopMost = true;
    }

    /// <summary>
    /// Sets the scene bitmap to display. The form takes ownership and will dispose
    /// the previous scene. Pass null to clear.
    /// </summary>
    public void SetScene(Bitmap scene)
    {
        var prev = _scene;
        _scene = scene;
        prev?.Dispose();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_scene == null) return;
        e.Graphics.DrawImage(_scene, 0, 0, ClientSize.Width, ClientSize.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _scene?.Dispose();
        base.Dispose(disposing);
    }
}
