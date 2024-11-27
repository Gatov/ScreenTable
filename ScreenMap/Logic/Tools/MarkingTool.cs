using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Windows.Input;
using ScreenMap.Controls;

namespace ScreenMap.Logic.Tools;

public class MarkingTool : DefaultTool
{
    private Color[] _colors =
    {
        Color.Chartreuse, Color.Brown, Color.Khaki, Color.Magenta, Color.Red, Color.Blue, 
        Color.DimGray
    };
    private int CurrentBrushIndex = 0;
    private PointF _lastKnownLocation;

    public MarkingTool(GmMap gmMap, IZoomable view) : base(gmMap, view)
    {
    }

    public override void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if(buttons == MouseButtons.Left)
            MarkAt(unscaledPos, modifiers);
        else base.OnMouseDown(unscaledPos, buttons, modifiers);
    }

    private void MarkAt(PointF unscaledPos, Keys modifiers)
    {
        if(modifiers == Keys.None)
            _gmMap.MarkAt(unscaledPos, BrushSize, Color.FromArgb(40,_colors[CurrentBrushIndex]).ToArgb());
        else if(modifiers == Keys.Control)
            _gmMap.RemoveMarkAt(unscaledPos);
        
        
    }

    public override void OnMouseWheel(int ticks, Keys modifiers)
    {
        int shift = ticks / 120;
        CurrentBrushIndex = (_colors.Length +CurrentBrushIndex+ shift % _colors.Length) % _colors.Length;
        Invalidate(_lastKnownLocation.RectByCenter(BrushSize));
    }
    public override void OnPaint(Graphics graphics, Point unscaledCursorPoint)
    {
        var old = graphics.CompositingMode;
        graphics.CompositingMode = CompositingMode.SourceOver;
        using var brush = new SolidBrush(Color.FromArgb(30, _colors[CurrentBrushIndex]));
        var fp = (PointF)unscaledCursorPoint;
        graphics.FillEllipse(brush, fp.RectByCenter(BrushSize));
        graphics.CompositingMode = old;

    }

    public override void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
    }

    public override void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        Invalidate(_lastKnownLocation.RectByCenter(BrushSize));
        _lastKnownLocation = unscaledPos;
        Invalidate(_lastKnownLocation.RectByCenter(BrushSize));
    }
    public string Hint => "LMB to add mark, Ctrl-MLB to remove, MWheel to change color";
}