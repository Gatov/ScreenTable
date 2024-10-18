using System.Diagnostics;
using Newtonsoft.Json;
using ScreenTable.Tools;
using Timer = System.Windows.Forms.Timer;


namespace ScreenTable;

public sealed partial class GMControl : UserControl
{
    private Image Bmp => _foggedMap?.OriginalImage;

    private Mode _mode = Mode.None;
    private readonly Timer _timer;
    readonly Stopwatch _lastDraw = new Stopwatch();
    private ToolCalibrate _calibrationTool;
    private ITool _currentTool;
    public event Action NeedRepaint;
    private int BrushSize =>(int) ((_mapInfo?.CellSize??20) * 4);
    private PointF _previousLocation= PointF.Empty;
    private double Density => BrushSize/8.0;
    private PlayersView _playerView;
    private MapInfo _mapInfo;
    private float _currentPlayerZoom = 1;
    private float _currentGmZoom = 1;
    private FoggedMap _foggedMap;

    public event Action<MapInfo> MapInfoChanged; 


    public GMControl()
    {
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseWheel += OnMouseWheel;
        KeyPress += OnKeyPress;
        //KeyDown += OnKeyDown;
        //KeyUp += OnKeyUp;
        AllowDrop = true;
        DragOver += (_, args) =>
        {
            if(args.Data is DataObject data && data.ContainsFileDropList() && data.GetFileDropList().Count == 1)
                args.Effect = DragDropEffects.Copy;
        };
        DragDrop += OnDragDrop;
        DoubleBuffered = true;
        _timer = new Timer();
        _timer.Tick += TimerOnTick;
        _timer.Start();
       
       
        NeedRepaint += ()=> _playerView?.Invalidate();
    }
    private void OnKeyPress(object sender, KeyPressEventArgs e)
    {
        switch (e.KeyChar)
        {
            case 'r': _currentGmZoom = 1;
                e.Handled = true;
                Invalidate();
                break;
            case 'f': 
                var aspect = Math.Min((float)ClientSize.Width/Bmp.Width, (float)ClientSize.Height/Bmp.Height);
                _currentGmZoom = (float)Math.Max(0.1,Math.Round(aspect, 1));
                e.Handled = true;
                Invalidate();
                break;
            case 's': // save
                Save();
                break;
        }
    }

    private void Save()
    {
        var text = JsonConvert.SerializeObject(_mapInfo, Formatting.Indented);
        var jsonFile = Path.ChangeExtension(_mapInfo.FileName, ".json"); 
        File.WriteAllText(jsonFile!, text);
    }

    public event Action<Mode> OnModeChanged;

    private void SetMode(Mode mode)
    {
        if (_mode != mode)
        {
            _mode = mode;
            OnModeChanged?.Invoke(_mode);
            Invalidate();
        }
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

    private void OnDragDrop(object sender, DragEventArgs e)
    {
        if (e.Data is DataObject data && data.ContainsFileDropList() && data.GetFileDropList().Count == 1)
        {
            try
            {
                var fileDrop = data.GetFileDropList()[0];
                if (fileDrop != null)
                {
                    _foggedMap?.Dispose();
                    try
                    {
                        UseWaitCursor = true;
                        LoadMap(fileDrop);
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

    private void LoadMap(string fileDrop)
    {
        if (Path.GetExtension(fileDrop) == ".json")
        {
            string text = File.ReadAllText(fileDrop);
            _mapInfo = JsonConvert.DeserializeObject<MapInfo>(text);
        }
        else
        {
            _mapInfo = new MapInfo() { FileName = fileDrop };
        }

        if (File.Exists(_mapInfo.FileName) == false) // files were moved, combine new path with filename
            _mapInfo.FileName = Path.Combine(Path.GetDirectoryName(fileDrop)!, Path.GetFileName(_mapInfo.FileName)!);

        _calibrationTool = new ToolCalibrate(_mapInfo);
        _calibrationTool.RequiresRepaint += CurrentToolOnRequiresRepaint;
        _foggedMap = new FoggedMap(_mapInfo.FileName);
        _foggedMap.OnRectUpdated += unscaledRect =>
        {
            var scaledRect = TranslateToScaledRect(unscaledRect);
            Invalidate(Rectangle.Ceiling(scaledRect));
        };
        _playerView?.Close();
        _playerView = new PlayersView(_foggedMap.PlayersImage, _mapInfo);

        _playerView.SetCenter(new PointF(Bmp.Width/2f, Bmp.Height/2f));
        _playerView.SetZoom(_currentPlayerZoom);
        _playerView.Show();
        MapInfoChanged?.Invoke(_mapInfo);
        Invalidate();
    }

    private void CurrentToolOnRequiresRepaint(RectangleF obj)
    {
        if(obj.IsEmpty)
            Invalidate();
        else
            Invalidate(Rectangle.Ceiling(TranslateToScaledRect(obj)));
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        if (_currentTool != null)
        {
            _currentTool.OnMouseWheel(e.Delta, ModifierKeys);
            return;
        }
        if (ModifierKeys == Keys.None)
        {
            
            var oldRect = _playerView.GetViewAreaInOriginal();
            _currentPlayerZoom = Math.Max(0.2f, Math.Min(5, _currentPlayerZoom + e.Delta / 1200.0f));
            _playerView.SetZoom(_currentPlayerZoom);
            var rect = _playerView.GetViewAreaInOriginal();

            var toRedraw = e.Delta > 0 ? oldRect : rect;
            
            toRedraw.Inflate(10, 10);
            
            Invalidate(Rectangle.Ceiling(TranslateToScaledRect(toRedraw)));
            //Invalidate();
        }
        else if(ModifierKeys == Keys.Shift)
        {
            _currentGmZoom = Math.Max(0.1f, Math.Min(10, _currentGmZoom + e.Delta / 1200.0f));
            Invalidate();
        }

    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if(Bmp == null) return;
        
        if(_lastDraw.IsRunning && _lastDraw.Elapsed > TimeSpan.FromSeconds(0.2))
        {
            _lastDraw.Stop();
            NeedRepaint?.Invoke();
        }
        
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        if(_foggedMap == null) return;
        Graphics g = e.Graphics;
        g.ScaleTransform(_currentGmZoom, _currentGmZoom);
        Rectangle clipRect = e.ClipRectangle;

        Stopwatch sw = Stopwatch.StartNew();
        var unscaledRect = new Rectangle(TranslateToUnscaledPoint(clipRect.Location),
            new Size(TranslateToUnscaledPoint(new Point(clipRect.Size))));
        unscaledRect.Inflate(1,1);
        g.SetClip(unscaledRect);
        bool drawFog = _mode != Mode.Calibrate;
        _foggedMap.Draw(g, unscaledRect, drawFog);
        g.DrawRectangle(Pens.Aquamarine, Rectangle.Ceiling(_playerView.GetViewAreaInOriginal()));
        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"GMControl.Paint: {sw.Elapsed} - {unscaledRect}");
        if(_currentTool != null)
            _currentTool.OnPaint(g);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (_currentTool != null)
        {
            _currentTool.OnMouseDown(unscaledPoint, e.Button, ModifierKeys);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            // check if Shift is pressed
            if (ModifierKeys == Keys.None)
            {
                SetMode(Mode.Reveal);
                RevealAt(unscaledPoint);
            }
            
        }
        else if(e.Button == MouseButtons.Middle)
        {
            SetMode(Mode.Pan);
            _playerView.SetCenter(unscaledPoint);
            Invalidate();
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (_currentTool != null)
        {
            _currentTool.OnMouseMove(unscaledPoint,e.Button, ModifierKeys);
            return;
        }
        
        if (e.Button == MouseButtons.Middle && _mode == Mode.Pan)
        {
            _mapInfo.Center = unscaledPoint;
            _playerView.SetCenter(unscaledPoint);
            Invalidate();
            return;
        }


        if (e.Button == MouseButtons.Left)
        {
            if (ModifierKeys == Keys.None && _mode == Mode.Reveal)
            {
                RevealingMove(unscaledPoint);
                return;
            }
        }

        //SetMode( Mode.None); // conditions have changed, reset
    }
    private void RevealingMove(Point unscaledPoint)
    {
        if (_previousLocation.IsEmpty )
        {
            RevealAt(unscaledPoint);
            return;
        }


        var prevLoc = _previousLocation;
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
                System.Diagnostics.Debug.WriteLine($"{newUnscaled}, {distance}");
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
           
        }
    }

    private void RevealAt(PointF unscaledPoint)
    {
        _foggedMap.RevealAt(unscaledPoint, BrushSize);
        _lastDraw.Restart();
        _previousLocation = unscaledPoint;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);

        if (_currentTool != null)
        {
            _currentTool.OnMouseUp(unscaledPoint,e.Button, ModifierKeys);
            return;
        }
        
        
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
        {
            //if(_mode == Mode.Calibrate)
              //  FinishCalibrating();
            //else
                SetMode(Mode.None);
        }
    }
    public void Calibration(bool calibrateChecked)
    {
        if (calibrateChecked)
        {
           SetMode(Mode.Calibrate);
           _currentTool = _calibrationTool;

        }
        else
        {
            //FinishCalibrating();
            MapInfoChanged?.Invoke(_mapInfo);
            _playerView.UpdateMapInfo(_mapInfo);
            SetMode(Mode.None);
            _currentTool = null;
            Save();
        }
    }
}