namespace ScreenTable.Tools;

public class ToolCalibrate : ITool
{
    private MapInfo _doc;


    private readonly int _minCalibrationDistance = 40;
    private MapInfo _mapInfo;
    private float _calibrationCellSize = 0;
    private PointF _calibrationStart;
    private PointF _calibrationCurrent;
    private bool _inSelection = false;
    private float _calibrationCells = 8;

    public ToolCalibrate(MapInfo doc)
    {
        _doc = doc;
    }

    public void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Shift))
        {
            _calibrationStart = unscaledPos;
            _inSelection = true;
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
        }

    }

    public void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Shift))
        {
            _calibrationCurrent = unscaledPos;
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
            _inSelection = false;
        }
    }

    public void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Shift) && _inSelection)
        {
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
        }
    }

    public void OnMouseWheel(int ticks, Keys modifiers)
    {
    }

    public event Action<RectangleF> RequiresRepaint;

    public void OnPaint(Graphics graphics)
    {
        //graphics.ScaleTransform(1, 1);
        float minDistance = Math.Min(_calibrationCurrent.X - _calibrationStart.X,
            _calibrationCurrent.Y - _calibrationStart.Y);
        // we should fit at least _calibrationCells in this distance, se, limit the distance to be minimum _minCalibrationDistance
        using var pen = new Pen(Color.FromArgb(40, Color.Yellow), 1);
        using var cross = new Pen(Color.Orange, 1);
        var width = graphics.ClipBounds.Right;
        var height = graphics.ClipBounds.Bottom;

        if (minDistance >= _minCalibrationDistance && !_inSelection)
        {
            _calibrationCellSize = minDistance / _calibrationCells; // we will fit 5 cells in there
            float xOffset = _calibrationStart.X % _calibrationCellSize;
            float yOffset = _calibrationStart.Y % _calibrationCellSize;
            // draw a grid
            for (float x = xOffset; x < width; x += _calibrationCellSize)
                graphics.DrawLine(pen, x, 0, x, height);
            for (float y = yOffset; y < height; y += _calibrationCellSize)
                graphics.DrawLine(pen, 0, y, width, y);
        }

        // just draw cross-hair
        graphics.DrawLine(cross, _calibrationStart.X, 0, _calibrationStart.X, height);
        graphics.DrawLine(cross, 0, _calibrationStart.Y, width, _calibrationStart.Y);
        graphics.DrawLine(cross, _calibrationCurrent.X, 0, _calibrationCurrent.X, height);
        graphics.DrawLine(cross, 0, _calibrationCurrent.Y, width, _calibrationCurrent.Y);

    }

}
