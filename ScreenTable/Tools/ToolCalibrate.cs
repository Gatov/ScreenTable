namespace ScreenTable.Tools;

public class ToolCalibrate : ITool
{
    //private MapInfo _doc;


    private readonly int _minCalibrationCellSize = 2;
    private MapInfo _mapInfo;
    private float _calibrationCellSize = 0;
    private PointF _calibrationStart;
    private PointF _calibrationCurrent;
    private bool _inSelection = false;
    private float _calibrationCells = 8;

    public ToolCalibrate(MapInfo doc)
    {
        _mapInfo = doc;
    }

    public void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        //if (modifiers.HasFlag(Keys.Shift))
        {
            _calibrationStart = unscaledPos;
            _inSelection = true;
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
        }

    }

    public void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        //if (modifiers.HasFlag(Keys.Shift))
        {
            _calibrationCurrent = unscaledPos;
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
            _inSelection = false;
        }
    }

    public void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (/*modifiers.HasFlag(Keys.Shift) && */_inSelection)
        {
            _calibrationCurrent = unscaledPos;
            
            RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
        }
    }

    public void OnMouseWheel(int ticks, Keys modifiers)
    {
        if (modifiers.HasFlag(Keys.Shift))
        {
            _calibrationCellSize *= (1+0.002f * ticks / 120);
        }
        else
        {
            // ReSharper disable once PossibleLossOfFraction
            _calibrationCells += ticks / 120;
            _calibrationCells = Math.Max(2, _calibrationCells);

            float minDistance = Math.Min(_calibrationCurrent.X - _calibrationStart.X,
                _calibrationCurrent.Y - _calibrationStart.Y);
            _calibrationCellSize = minDistance / _calibrationCells; // we will fit 5 cells in there
        }

        UpdateInfo();
        RequiresRepaint?.Invoke(RectangleF.Empty); // full repaint
    }

    private void UpdateInfo()
    {
        if (_calibrationCellSize > 0)
        {
            _mapInfo.OffsetX = (int)(_calibrationStart.X % _calibrationCellSize);
            _mapInfo.OffsetY = (int)(_calibrationStart.Y % _calibrationCellSize);
            _mapInfo.CellSize = _calibrationCellSize;
        }
    }

    public event Action<RectangleF> RequiresRepaint;

    public void OnPaint(Graphics graphics)
    {
        //graphics.ScaleTransform(1, 1);
        //float minDistance = Math.Min(_calibrationCurrent.X - _calibrationStart.X,
         //   _calibrationCurrent.Y - _calibrationStart.Y);
        // we should fit at least _calibrationCells in this distance, se, limit the distance to be minimum _minCalibrationDistance
        using var pen = new Pen(Color.FromArgb(40, Color.Yellow), 1);
        using var cross = new Pen(Color.Orange, 1);
        var width = graphics.ClipBounds.Right;
        var height = graphics.ClipBounds.Bottom;

        if (_calibrationCellSize >= _minCalibrationCellSize && !_inSelection)
        {
            //_calibrationCellSize = minDistance / _calibrationCells; // we will fit 5 cells in there
            float xOffset = _mapInfo.OffsetX;
            float yOffset = _mapInfo.OffsetY;
            // draw a grid
            for (float x = xOffset; x < width; x += _mapInfo.CellSize)
                graphics.DrawLine(pen, x, 0, x, height);
            for (float y = yOffset; y < height; y += _mapInfo.CellSize)
                graphics.DrawLine(pen, 0, y, width, y);
        }

        // just draw cross-hair
        graphics.DrawLine(cross, _calibrationStart.X, 0, _calibrationStart.X, height);
        graphics.DrawLine(cross, 0, _calibrationStart.Y, width, _calibrationStart.Y);
        graphics.DrawLine(cross, _calibrationCurrent.X, 0, _calibrationCurrent.X, height);
        graphics.DrawLine(cross, 0, _calibrationCurrent.Y, width, _calibrationCurrent.Y);

    }

}
