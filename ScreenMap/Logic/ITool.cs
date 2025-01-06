using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils.Drawing;

namespace ScreenMap.Logic;

public interface ITool
{
    void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseWheel(int ticks, Keys modifiers);
    void OnPaint(Graphics graphics, Point unscaledCursorPoint);
    event Action<RectangleF> RequiresRepaint;
    bool DrawFog { get; }
    string Hint { get; }
}

public interface IKeyTool
{
    void OnKeyDown(KeyEventArgs e);
}