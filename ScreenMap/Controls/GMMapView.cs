using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils;
using DevExpress.Utils.Extensions;
using ScreenMap.Logic;
using ScreenMap.Logic.Tools;
using ScreenTable.Tools;

namespace ScreenMap.Controls;

public partial class GmMapView : DevExpress.XtraEditors.XtraUserControl, IZoomable
{

    private readonly GmMap _map;

    private float _currentGmZoom = 1;
    private float _brushSize = 3;
    // tools
    private ToolCalibrate _toolCalibrate;
    private DefaultTool _defaultTool;
    private MarkingTool _markingTool;

    private ITool _currentTool = null;
    public event Action<ITool> ToolChange; 
    
    public float ZoomLevel
    {
        get => _currentGmZoom;
        set
        {
            _currentGmZoom = value;
            if (_map != null)
                Size = _map.Size.Scale(_currentGmZoom);
            Invalidate();
        }
    }
    public GmMap Map => _map;

    public GmMapView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        //Paint += OnPaint;
        PaintEx += OnPaintEx;
            
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseWheel += OnMouseWheel;
            
        DragOver += (_, args) =>
        {
            if (args.Data is DataObject data && data.ContainsFileDropList() && data.GetFileDropList().Count == 1)
                args.Effect = DragDropEffects.Copy;
        };
        AllowDrop = true;
        _map = new GmMap();
        DragDrop += OnDragDrop;
    }

    private void OnMouseWheel(object sender, MouseEventArgs e)
    {
        if(_map == null) return;
        if (_currentTool != null)
        {
            _currentTool.OnMouseWheel(e.Delta, ModifierKeys);
            return;
        }
    }

    private void OnMouseUp(object sender, MouseEventArgs e)
    {
        if(_map == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (_currentTool != null)
        {
            _currentTool.OnMouseUp(unscaledPoint,e.Button, ModifierKeys);
            return;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if(_map == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (_currentTool != null)
        {
            _currentTool.OnMouseMove(unscaledPoint,e.Button, ModifierKeys);
            return;
        }
    }


    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }
    private void OnPaintEx(object sender, XtraPaintEventArgs e)
    {
        if(_map == null)
            return;
        //     throw new NotImplementedException();
        // }
        // protected override void OnDirectXPaint(GraphicsCache cache, UserLookAndFeel activeLookAndFeel)
        // {
        var g = e.Cache;
        Rectangle clipRect = e.ClipRectangle;
        //g.FillRectangle(Brushes.Aqua, new Rectangle(10, 10, 300, 300));
        if (_map == null) return;
        g.ScaleTransform(_currentGmZoom, _currentGmZoom);

        Stopwatch sw = Stopwatch.StartNew();
        var unscaledRect = new Rectangle(TranslateToUnscaledPoint(clipRect.Location),
            new Size(TranslateToUnscaledPoint(new Point(clipRect.Size))));
        unscaledRect.Inflate(1, 1);
        g.SetClip(unscaledRect);
        bool drawFog = _currentTool?.DrawFog !=false;
        _map.Draw(g, unscaledRect, drawFog);
        //TODO: g.DrawRectangle(Pens.Aquamarine, Rectangle.Ceiling(_playerView.GetViewAreaInOriginal()));
        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"GMControl.Paint: {sw.Elapsed} - {unscaledRect}");
        if (_currentTool != null)
            _currentTool.OnPaint(g.Graphics, TranslateToUnscaledPoint(PointToClient(MousePosition)));
    }
    private void OnDragDrop(object sender, DragEventArgs e)
    {
        if (e.Data is DataObject data && data.ContainsFileDropList() && data.GetFileDropList().Count == 1)
        {
            try
            {
                var fileDrop = data.GetFileDropList()[0];
                if (fileDrop != null)
                {
                    try
                    {
                        UseWaitCursor = true;
                        _map.LoadMap(fileDrop);
                        
                        //this.Size = _map.OriginalImage.Size;
                        ZoomLevel = 1; // reset
                        InitializeTools();
                        Invalidate();
                    }
                    finally
                    {
                        UseWaitCursor = false;
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }
    }
    private void InitializeTools()
    {

        _toolCalibrate = new ToolCalibrate(_map);
        _toolCalibrate.RequiresRepaint += CurrentToolOnRequiresRepaint;
        _defaultTool = new DefaultTool(_map, this);
        _defaultTool.SetBrushSize(_brushSize);
        _defaultTool.RequiresRepaint+= CurrentToolOnRequiresRepaint;
        
        _markingTool = new MarkingTool(_map, this);
        _markingTool.SetBrushSize(_brushSize);
        _markingTool.RequiresRepaint+= CurrentToolOnRequiresRepaint;
        
        SetTool(_defaultTool);
    }
    private void CurrentToolOnRequiresRepaint(RectangleF obj)
    {
        if (obj.IsEmpty)
            Invalidate();
        else
            Invalidate(Rectangle.Ceiling(TranslateToScaledRect(obj)));
    }

    Point TranslateToUnscaledPoint(Point point)
    {
        return new Point((int)Math.Round(point.X / _currentGmZoom), (int)Math.Round(point.Y / _currentGmZoom));
    }
    PointF TranslateToScaledPoint(PointF unscaledPoint)
    {
        return new PointF(unscaledPoint.X * _currentGmZoom, unscaledPoint.Y * _currentGmZoom);
    }
    RectangleF TranslateToScaledRect(RectangleF uRc)
    {
        return new RectangleF(uRc.X * _currentGmZoom, uRc.Y * _currentGmZoom,
            uRc.Width * _currentGmZoom, uRc.Height * _currentGmZoom);
    }
    
    
    private void OnMouseDown(object sender, MouseEventArgs e)
    {
        if(_map == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (_currentTool != null)
        {
            _currentTool.OnMouseDown(unscaledPoint, e.Button, ModifierKeys);
            return;
        }
    }

    public void CalibrationMode(bool calibrate)
    {
        if (calibrate)
            SetTool(_toolCalibrate);
        else
            SetTool(_defaultTool);
    }

    public void SaveFile()
    {
        _map.SaveMapInfo();
    }

    public void SetBrushSize(float brushSizeInCells)
    {
        _brushSize = brushSizeInCells;
        _defaultTool?.SetBrushSize(brushSizeInCells);
        _markingTool?.SetBrushSize(brushSizeInCells);
    }

    public void MarkingMode(bool mark)
    {
        if (mark)
            SetTool(_markingTool);
        else
            SetTool(_defaultTool);
        
    }

    private void SetTool(ITool tool)
    {
        _currentTool = tool;
        ToolChange?.Invoke(tool);
        Invalidate();
    }
}

public interface IZoomable
{
    float ZoomLevel { get; set; }
}