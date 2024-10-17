namespace ScreenTable;

public interface ITool
{
    void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers);
    void OnMouseWheel(int ticks, Keys modifiers);
    event Action<RectangleF> RequiresRepaint;
}