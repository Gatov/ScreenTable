using System;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils.Drawing;
using ScreenMap.Controls;

namespace ScreenMap.Logic.Tools;

public class DefaultTool : ITool
{
    protected readonly GmMap _gmMap;
    protected readonly MapInfo _mapInfo;
    private readonly IZoomable _view;
    private PointF _previousReveal;

    protected int BrushSize =>(int) ((_mapInfo?.CellSize??20) * _brushSizeInCells);
    private float _brushSizeInCells = 4;
    private double Density => BrushSize/8.0;
    
    public DefaultTool(GmMap gmMap, IZoomable view)
    {
        _gmMap = gmMap;
        _mapInfo = gmMap.Info;
        _view = view;
        _gmMap.OnRectUpdated += rc =>RequiresRepaint?.Invoke(rc);
    }
    public virtual void OnMouseDown(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if(buttons == MouseButtons.Left)
            RevealAt(unscaledPos, modifiers);
        else if(buttons == MouseButtons.Middle && modifiers == Keys.None)
        {
            _mapInfo.Center = Point.Round(unscaledPos);
            //RequiresRepaint?.Invoke(RectangleF.Empty);
            _gmMap.CenterAt(unscaledPos);
        }
    }

    private void RevealAt(PointF unscaledPos, Keys modififiers)
    {
        bool reveal;
        switch (modififiers)
        {
            case Keys.None: { reveal = true; break; }
            case Keys.Shift: { reveal = false; break; }
            default: return; // not a recognized key
        }
        _gmMap.RevealAt(Point.Round(unscaledPos), BrushSize, reveal);
        _previousReveal = unscaledPos;
        RequiresRepaint?.Invoke(unscaledPos.RectByCenter(BrushSize));
    }

    public virtual void OnMouseUp(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if(buttons == MouseButtons.Left && unscaledPos!= _previousReveal)
            RevealAt(unscaledPos, modifiers);
    }

    public virtual void OnMouseMove(PointF unscaledPos, MouseButtons buttons, Keys modifiers)
    {
        if (buttons == MouseButtons.Left)
            RevealingMove(unscaledPos, modifiers);
    }

    public virtual void OnMouseWheel(int ticks, Keys modifiers)
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

    public virtual void OnPaint(Graphics graphics)
    {
        //throw new NotImplementedException();
    }
    private void RevealingMove(PointF unscaledPoint, Keys modifiers)
    {
        if (_previousReveal.IsEmpty )
        {
            RevealAt(unscaledPoint, modifiers);
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
                RevealAt(newUnscaled, modifiers);
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
        Invalidate(rc);
    }

    protected void Invalidate(RectangleF rc)
    {
        RequiresRepaint?.Invoke(rc);
    }

    public event Action<RectangleF> RequiresRepaint;
    public bool DrawFog => true;

    public void SetBrushSize(float sizeInCells)
    {
        _brushSizeInCells = sizeInCells;
    }
}