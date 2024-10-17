using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Timer = System.Windows.Forms.Timer;


namespace ScreenTable;

public sealed partial class GMControl : UserControl
{
    private Image Bmp => _foggedMap?.OriginalImage;
    //private Bitmap _gmOverlay;

    private Mode _mode = Mode.None;
    private readonly Timer _timer;
    readonly Stopwatch _lastDraw = new Stopwatch();
    public event Action NeedRepaint;
    private readonly int _brushSize = 50;
    //private Bitmap _playersImage;
    //private TextureBrush _revealBrush;
    //private TextureBrush _semiRevealBrush;
    private PointF _previousLocation= PointF.Empty;
    private readonly double _density = 3;
    private PlayersView _playerView;
    private Point _calibrationStart;
    private Point _calibrationCurrent;
    private int _calibrationCells = 8;
    private readonly int _minCalibrationDistance = 40;
    private MapInfo _mapInfo;
    private float _calibrationCellSize = 0;
    private float _currentPlayerZoom = 1;
    private float _currentGmZoom = 1;
    private FoggedMap _foggedMap;


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
/*
    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if(e.KeyData.HasFlag(Keys.Shift))
        {
            Invalidate();
            e.Handled = true;
        }
    }*/

/*    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if(e.KeyData.HasFlag(Keys.Shift))
        {
            Invalidate();
            e.Handled = true;
        }
    }*/

    private void OnKeyPress(object sender, KeyPressEventArgs e)
    {
        switch (e.KeyChar)
        {
            case 'r': _currentGmZoom = 1;
                e.Handled = true;
                break;
            case 'f': 
                var aspect = Math.Min((float)ClientSize.Width/Bmp.Width, (float)ClientSize.Height/Bmp.Height);
                _currentGmZoom = (float)Math.Max(0.1,Math.Round(aspect, 1));
                e.Handled = true;
                break;
            case 's': // save
                var text = JsonConvert.SerializeObject(_mapInfo, Formatting.Indented);
                var jsonFile = Path.ChangeExtension(_mapInfo.FileName, ".json"); 
                File.WriteAllText(jsonFile!, text);
                break;
        }
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
                    LoadMap(fileDrop);
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
        Invalidate();
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        if (_mode == Mode.Calibrate)
        {
            _calibrationCells += e.Delta/120;
            _calibrationCells = Math.Max(2, _calibrationCells);
            Invalidate();
            return;
        }
        
        if (ModifierKeys == Keys.None)
        {
            
            var oldRect = Rectangle.Ceiling(_playerView.GetViewAreaInOriginal());
            _currentPlayerZoom = Math.Max(0.2f, Math.Min(5, _currentPlayerZoom + e.Delta / 1200.0f));
            _playerView.SetZoom(_currentPlayerZoom);
            var rect = Rectangle.Ceiling(_playerView.GetViewAreaInOriginal());

            var toRedraw = e.Delta > 0 ? oldRect : rect;
            
            toRedraw.Inflate(1, 1);
            Invalidate(toRedraw);
        }
        else if(ModifierKeys == Keys.Shift)
        {
            _currentGmZoom = Math.Max(0.2f, Math.Min(5, _currentGmZoom + e.Delta / 1200.0f));
            Invalidate();
        }

    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if(Bmp == null) return;
        if(_lastDraw.Elapsed < TimeSpan.FromSeconds(0.2))
        {
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
        //g.CompositingQuality = CompositingQuality.HighSpeed;
        //g.InterpolationMode = InterpolationMode.Low;
        Rectangle clipRect = e.ClipRectangle;

        Stopwatch sw = Stopwatch.StartNew();
        var unscaledRect = new Rectangle(TranslateToUnscaledPoint(clipRect.Location),
            new Size(TranslateToUnscaledPoint(new Point(clipRect.Size))));
        unscaledRect.Inflate(1,1);
        g.SetClip(unscaledRect);
        bool drawFog = _mode != Mode.Calibrate;
        _foggedMap.Draw(g, unscaledRect, drawFog);
        //g.DrawImage(_bmp, unscaledRect, unscaledRect, GraphicsUnit.Pixel);
        System.Diagnostics.Debug.WriteLine($"GMControl.Paint 1: {sw.Elapsed} - {unscaledRect}");
        //if (_mode != Mode.Calibrate)
        //    g.DrawImage(_gmOverlay, unscaledRect, unscaledRect, GraphicsUnit.Pixel);
        System.Diagnostics.Debug.WriteLine($"GMControl.Paint 2: {sw.Elapsed} - {unscaledRect}");
        g.DrawRectangle(Pens.Aquamarine, Rectangle.Ceiling(_playerView.GetViewAreaInOriginal()));
        sw.Stop();
        System.Diagnostics.Debug.WriteLine($"GMControl.Paint 3: {sw.Elapsed} - {unscaledRect}");
        if (_mode == Mode.Calibrate)
            PaintCalibration(g);
        // invert the alpha channel of _overlay and draw it

    }

    private void PaintCalibration(Graphics graphics)
    {
        graphics.ScaleTransform(1, 1);
        float minDistance = Math.Min(_calibrationCurrent.X - _calibrationStart.X, _calibrationCurrent.Y - _calibrationStart.Y);
        // we should fit at least _calibrationCells in this distance, se, limit the distance to be minimum _minCalibrationDistance
        using var pen = new Pen(Color.FromArgb(40,Color.Yellow), 1);
        using var cross = new Pen(Color.Orange, 1);
        if(minDistance >= _minCalibrationDistance)
        {
            _calibrationCellSize = minDistance / _calibrationCells; // we will fit 5 cells in there
            float xOffset = _calibrationStart.X % _calibrationCellSize;
            float yOffset = _calibrationStart.Y % _calibrationCellSize;
            // draw a grid
            for(float x = xOffset; x < ClientSize.Width; x+=_calibrationCellSize)
                graphics.DrawLine(pen, x, 0, x, ClientSize.Height);
            for(float y = yOffset; y < ClientSize.Height; y+=_calibrationCellSize)
                graphics.DrawLine(pen, 0, y, ClientSize.Width, y);
        }
        // just draw cross-hair
        graphics.DrawLine(cross, _calibrationStart.X, 0, _calibrationStart.X, ClientSize.Height);
        graphics.DrawLine(cross, 0, _calibrationStart.Y, ClientSize.Width, _calibrationStart.Y);
        graphics.DrawLine(cross, _calibrationCurrent.X, 0, _calibrationCurrent.X, ClientSize.Height);
        graphics.DrawLine(cross, 0, _calibrationCurrent.Y, ClientSize.Width, _calibrationCurrent.Y);

    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        var unscaledPoint = TranslateToUnscaledPoint(e.Location);
        if (e.Button == MouseButtons.Left)
        {
            if (_mode == Mode.Calibrate)
            {
                StartCalibrating(unscaledPoint);
                return;
            }
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
        var mapCoordinates = TranslateToUnscaledPoint(e.Location);
        
        if (e.Button == MouseButtons.Middle && _mode == Mode.Pan)
        {
            _playerView.SetCenter(mapCoordinates);
            Invalidate();
            return;
        }


        if (e.Button == MouseButtons.Left)
        {
            if(_mode == Mode.Calibrate)
            {
                CalibratingMove(mapCoordinates);
                return;
            }
            
            if (ModifierKeys == Keys.None && _mode == Mode.Reveal)
            {
                RevealingMove(mapCoordinates);
                return;
            }
        }

        //SetMode( Mode.None); // conditions have changed, reset
    }

    private void StartCalibrating(Point unscaledPoint)
    {
        _calibrationStart = unscaledPoint;
    }
    private void CalibratingMove(Point unscaledPoint)
    {
        
        _calibrationCurrent = unscaledPoint;
        Invalidate();
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
        if(distance<_density) return;
        double cX = (unscaledPoint.X - prevLoc.X)/distance;
        double cY = (unscaledPoint.Y - prevLoc.Y)/distance;
        double progress = 0;
        while (distance > _density)
        {
            distance -= _density;
            progress += _density;
            var newUnscaled = new Point((int)(prevLoc.X + progress * cX), (int)(prevLoc.Y + progress * cY));
            try
            {
                RevealAt(newUnscaled);
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
        _foggedMap.RevealAt(unscaledPoint, _brushSize);
        _lastDraw.Restart();
        _previousLocation = unscaledPoint;
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if(Bmp == null) return;
        if (e.Button == MouseButtons.Left)
        {
            if(_mode == Mode.Calibrate)
                FinishCalibrating();
            else
                SetMode(Mode.None);
        }
    }

    private void FinishCalibrating()
    {
        if (_calibrationCellSize > 0)
        {
            _mapInfo.CellSize = _calibrationCellSize;
            _mapInfo.OffsetX = (int)(_calibrationStart.X % _calibrationCellSize);
            _mapInfo.OffsetY = (int)(_calibrationStart.Y % _calibrationCellSize);
            _playerView.UpdateMapInfo(_mapInfo);
        }
    }


   

    public void Calibration(bool calibrateChecked)
    {
        if (calibrateChecked)
            SetMode(Mode.Calibrate);
        else
        {
            FinishCalibrating();
            SetMode(Mode.None);
        }
    }
}