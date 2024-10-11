using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Timer = System.Windows.Forms.Timer;


namespace ScreenTable;

public sealed partial class GMControl : UserControl
{
    private Image _bmp;
    private Bitmap _gmOverlay;

    private Mode _mode = Mode.None;
    private readonly Timer _timer;
    readonly Stopwatch _lastDraw = new Stopwatch();
    public event Action NeedRepaint;
    private readonly int brushSize = 50;
    private Bitmap _playersImage;
    private TextureBrush _revealBrush;
    private TextureBrush _semiRevealBrush;
    private PointF _previousLocation= PointF.Empty;
    private readonly double _density = 3;
    private PlayersView _playerView;
    private Point _calibrationStart;
    private Point _calibrationCurrent;
    private readonly int _calibrationCells = 8;
    private readonly int _minCalibrationDistance = 40;
    private MapInfo _mapInfo;
    private float _calibrationCellSize = 0;
    private float _currentZoom = 1;


    public GMControl()
    {
        Paint += OnPaint;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseWheel += OnMouseWheel;
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

    public event Action<Mode> OnModeChanged;

    private void SetMode(Mode mode)
    {
        if (_mode != mode)
        {
            _mode = mode;
            OnModeChanged?.Invoke(_mode);
        }
    }

    private void LoadImage(Image newMap)
    {
        _bmp?.Dispose();
        _bmp = newMap;
        _gmOverlay?.Dispose();
        _gmOverlay = new Bitmap(_bmp.Width, _bmp.Height, PixelFormat.Format32bppArgb);
        _playerView?.Close();
        _playersImage?.Dispose();
        _playersImage = new Bitmap(_bmp.Width, _bmp.Height, PixelFormat.Format32bppArgb);
       
        using (Graphics g = Graphics.FromImage(_gmOverlay))
        {
            using (var brush = new SolidBrush(Color.FromArgb(200, Color.Gray)))
            {
                g.FillRectangle(brush, 0, 0, _gmOverlay.Width, _gmOverlay.Height);
            }
        }
        using (Graphics g = Graphics.FromImage(_playersImage))
        {
            using (var brush = new SolidBrush( Color.CadetBlue))
            {
                g.FillRectangle(brush, 0,0, _gmOverlay.Width, _gmOverlay.Height);
            }
        }
       
        _revealBrush = CreateSemitransparentBrushFromImage(_bmp, 0.8f);
        _semiRevealBrush = CreateSemitransparentBrushFromImage(_bmp, 0.2f);
        //NeedRepaint += Invalidate;
        // attmpt to load MapInfo
        _mapInfo = new MapInfo();
       
        _playerView = new PlayersView(_playersImage, _mapInfo);
        _playerView.SetCenter(new PointF(_bmp.Width/2f, _bmp.Height/2f));
        _playerView.SetZoom(_currentZoom);
        _playerView.Show();
        Invalidate();
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
                    var newMap = Image.FromFile(fileDrop);
                    LoadImage(newMap);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if(_bmp == null) return;
       
        _currentZoom = Math.Max(0.2f, Math.Min(5, _currentZoom + e.Delta / 1200.0f));
        _playerView.SetZoom(_currentZoom);
        var rect = Rectangle.Ceiling(_playerView.GetViewAreaInOriginal());
        rect.Inflate(1,1);
        Invalidate(rect);
       
    }

    private void TimerOnTick(object? sender, EventArgs e)
    {
        if(_bmp == null) return;
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
        if(_bmp == null) return;
        Graphics g = e.Graphics;
        Rectangle clipRect = e.ClipRectangle;
        g.SetClip(clipRect);
        g.DrawImage(_bmp, clipRect, clipRect, GraphicsUnit.Pixel);
       
        if (_mode != Mode.Calibrate)
            g.DrawImage(_gmOverlay, clipRect, clipRect, GraphicsUnit.Pixel);
        g.DrawRectangle(Pens.Aquamarine, Rectangle.Ceiling(_playerView.GetViewAreaInOriginal()));
        if (_mode == Mode.Calibrate)
            PaintCalibration(g);
        // invert the alpha channel of _overlay and draw it

    }

    private void PaintCalibration(Graphics graphics)
    {
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
        if(_bmp == null) return;
        if (e.Button == MouseButtons.Left)
        {
            // check if Shift is pressed
            if (ModifierKeys == Keys.None)
            {
                SetMode(Mode.Reveal);
                RevealAt(e.Location);
            }
            else if (ModifierKeys == Keys.Shift)
            {
                SetMode(Mode.Calibrate);
                StartCalibrating(e.Location);
            }
        }
        else if(e.Button == MouseButtons.Middle)
        {
            SetMode(Mode.Pan);
            _playerView.SetCenter(e.Location);
            Invalidate();
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if(_bmp == null) return;
        if (e.Button == MouseButtons.Middle && _mode == Mode.Pan)
        {
            _playerView.SetCenter(e.Location);
            Invalidate();
            return;
        }


        if (e.Button == MouseButtons.Left)
        {
            if (ModifierKeys == Keys.None && _mode == Mode.Reveal)
            {
                RevealingMove(e.Location);
                return;
            }
            else if (ModifierKeys == Keys.Shift && _mode == Mode.Calibrate)
            {
                CalibratingMove(e.Location);
                return;
            }
        }

        SetMode( Mode.None); // conditions have changed, reset
    }

    private void StartCalibrating(Point eLocation)
    {
        _calibrationStart = eLocation;
    }
    private void CalibratingMove(Point circlePosition)
    {
        _calibrationCurrent = circlePosition;
        Invalidate();
    }

    private void RevealingMove(Point circlePosition)
    {
        if (_previousLocation.IsEmpty )
        {
            RevealAt(circlePosition);
            return;
        }


        var prevLoc = _previousLocation;
        var distance = CalculateDistance(prevLoc, circlePosition);
        if(distance<_density) return;
        double cX = (circlePosition.X - prevLoc.X)/distance;
        double cY = (circlePosition.Y - prevLoc.Y)/distance;
        double progress = 0;
        while (distance > _density)
        {
            distance -= _density;
            progress += _density;
            var newCirclePosition = new PointF((float)(prevLoc.X + progress * cX), (float)(prevLoc.Y + progress * cY));
            try
            {
                RevealAt(newCirclePosition);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
           
        }
    }

    private void RevealAt(PointF circlePosition)
    {
        var rect = new RectangleF(circlePosition.X - brushSize / 2f, circlePosition.Y - brushSize / 2f, brushSize, brushSize);
        using (Graphics g = Graphics.FromImage(_gmOverlay))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            // create a radial brush with alpha increasing towards the center

            using (var brush = new SolidBrush(Color.FromArgb(0, Color.Aqua)))
            {
                //set anti-aliasing
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, rect);
                _previousLocation = circlePosition;
            }
        }

        using (Graphics g = Graphics.FromImage(_playersImage))
        {
            g.CompositingMode = CompositingMode.SourceOver;
            // set the _revealBrush to draw a portion of original image in place
            var smaller = rect;
            smaller.Inflate(-0.3f * brushSize, -0.3f * brushSize);
            g.FillEllipse(_semiRevealBrush, rect);
            g.FillEllipse(_revealBrush, smaller);
        }

        _lastDraw.Restart();
        Invalidate(new Rectangle((int)(circlePosition.X - 50), (int)(circlePosition.Y - 50), 100, 100));
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if(_bmp == null) return;
        if (e.Button == MouseButtons.Left)
        {
            if(_mode == Mode.Calibrate)
                FinishCalibrating();
            SetMode(Mode.None);
            Invalidate();
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


    TextureBrush CreateSemitransparentBrushFromImage(Image original, float transparency)
    {
        var texture = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb);
        var colorMatrix = new ColorMatrix { Matrix33 = transparency };
       
        ImageAttributes imageAttributes = new ImageAttributes();
        imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        using (Graphics g = Graphics.FromImage(texture))
        {
            var rect = new Rectangle(0, 0, original.Width, original.Height);
            g.DrawImage(original, rect, 0, 0, original.Width, original.Height, GraphicsUnit.Pixel, imageAttributes);
        }
        // create and return a textured brush from the texture
        return new TextureBrush(texture);
    }
    public static double CalculateDistance(PointF p1, PointF p2)
    {
        float dx = p2.X - p1.X;
        float dy = p2.Y - p1.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
   
}