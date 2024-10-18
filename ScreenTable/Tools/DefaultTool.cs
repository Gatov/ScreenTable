namespace ScreenTable.Tools;

public class DefaultTool : ITool
{
    private readonly FoggedMap _foggedMap;
    private readonly MapInfo _mapInfo;
    private PointF _previousReveal;

    private int BrushSize =>(int) ((_mapInfo?.CellSize??20) * 4);
    private double Density => BrushSize/8.0;
    
    public DefaultTool(FoggedMap foggedMap, MapInfo mapInfo)
    {
        _foggedMap = foggedMap;
        _mapInfo = mapInfo;
        _foggedMap.OnRectUpdated += rc =>RequiresRepaint?.Invoke(rc);
    }
    public void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if(modifiers == Keys.None && buttons == MouseButtons.Left)
            RevealAt(unscaledPos);
        else if(buttons == MouseButtons.Middle && modifiers == Keys.None)
        {
            _mapInfo.Center = Point.Round(unscaledPos);
            RequiresRepaint?.Invoke(RectangleF.Empty);
        }
    }

    private void RevealAt(PointF unscaledPos)
    {
        _foggedMap.RevealAt(Point.Round(unscaledPos), BrushSize);
        _previousReveal = unscaledPos;
    }

    public void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if(modifiers == Keys.None && buttons == MouseButtons.Left && unscaledPos!= _previousReveal)
            RevealAt(unscaledPos);
    }

    public void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (buttons == MouseButtons.Left && modifiers == Keys.None)
            RevealingMove(unscaledPos);
    }

    public void OnMouseWheel(int ticks, Keys modifiers)
    {
        //throw new NotImplementedException();
    }

    public void OnPaint(Graphics graphics)
    {
        //throw new NotImplementedException();
    }
    private void RevealingMove(PointF unscaledPoint)
    {
        if (_previousReveal.IsEmpty )
        {
            RevealAt(unscaledPoint);
            return;
        }


        var prevLoc = _previousReveal;
        var distance = FogUtil.CalculateDistance(prevLoc, unscaledPoint);
        if(distance<Density) return;
        double cX = (unscaledPoint.X - prevLoc.X)/distance;
        double cY = (unscaledPoint.Y - prevLoc.Y)/distance;
        double progress = 0;
        while (distance > Density)
        {
            distance -= Density;
            progress += Density;
            var newUnscaled = new Point((int)(prevLoc.X + progress * cX), (int)(prevLoc.Y + progress * cY));
            try
            {
                RevealAt(newUnscaled);
                //System.Diagnostics.Debug.WriteLine($"{newUnscaled}, {distance}");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        var rc = new RectangleF(prevLoc, new SizeF(_previousReveal));
        rc.Inflate(BrushSize/2f+1,BrushSize/2f+1);
        RequiresRepaint?.Invoke(rc);
    }
    public event Action<RectangleF> RequiresRepaint;
}