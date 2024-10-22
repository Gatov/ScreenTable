using System;
using System.Drawing;
using System.Windows.Forms;

namespace ScreenMap.Logic.Tools;

public class DefaultTool : ITool
{
    private readonly GMMap _gmMap;
    private readonly MapInfo _mapInfo;
    private readonly IZoomable _view;
    private PointF _previousReveal;

    private int BrushSize =>(int) ((_mapInfo?.CellSize??20) * 4);
    private double Density => BrushSize/8.0;
    
    public DefaultTool(GMMap gmMap, IZoomable view)
    {
        _gmMap = gmMap;
        _mapInfo = gmMap.Info;
        _view = view;
        _gmMap.OnRectUpdated += rc =>RequiresRepaint?.Invoke(rc);
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
        _gmMap.RevealAt(Point.Round(unscaledPos), BrushSize);
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
        if (modifiers == Keys.None)
        {
            _gmMap.ZoomStep(ticks / 120);
            // will invalidate and redraw on response from the client
        }
        else if(modifiers == Keys.Shift)
        {
            _view.ZoomLevel = Math.Max(0.1f, Math.Min(10, _view.ZoomLevel + ticks / 1200.0f));
        }
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