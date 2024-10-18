
using System.Drawing.Drawing2D;
namespace ScreenTable;
public sealed partial class PlayersView : Form
{
    private readonly Image _playersImage;
    private readonly MapInfo _mapInfo;
    private float _dpiX;
    private float _dpiY;
    private float _zoomBaseX;
    private float _zoomBaseY;
    private float _zoomFactor;
    private float ZoomX=>_zoomFactor*_zoomBaseX;
    private float ZoomY=>_zoomFactor*_zoomBaseY;
    private Point _currentPanOffset;
    private PointF _center;
    private readonly Color _gridColor = Color.FromArgb(128,Color.Yellow);

    public PlayersView(Image playersImage, MapInfo mapInfo)
    {
       
        _playersImage = playersImage;
        _mapInfo = mapInfo;
        DoubleBuffered = true;
        InitializeComponent();
        Paint += OnPaint;
        Load += OnLoad;
        SizeChanged += (sender, args) => UpdateVisibleArea();
        using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
        {
            _dpiX = g.DpiX;
            _dpiY = g.DpiY;
        }

        _zoomBaseX = _dpiX/mapInfo.CellSize;
        _zoomBaseY = _dpiY/mapInfo.CellSize;
        // create a gray pen of width 2
    }

    private void OnLoad(object sender, EventArgs e)
    {
        if (Screen.AllScreens.Length > 1)
        {
            var secondary = Screen.AllScreens.First(x => !x.Primary);
            Location = secondary.Bounds.Location;
            //Bounds = secondary.Bounds;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // do nothing
    }
/// <summary>
/// Visible Area is the area of the image that is currently visible in the control
/// </summary>
/// <param name="center"></param>
    public void SetCenter(PointF center)
    {
        _center = center;
        UpdateVisibleArea();
    }

    public void SetZoom(float zoom)
    {
        _zoomFactor = zoom;
        UpdateVisibleArea();
    }
    private void UpdateVisibleArea()
    {
        _currentPanOffset = new Point((int)(-_center.X+ClientSize.Width/2f/ZoomX), (int)(-_center.Y+ClientSize.Height/2f/ZoomY));
        Invalidate();
    }

    public RectangleF GetViewAreaInOriginal()
    {
        var width = ClientSize.Width/ZoomX;
        var height = ClientSize.Height/ZoomY;
        return new RectangleF(_center.X-width/2, _center.Y-height/2, width, height);
    }
    public void UpdateMapInfo(MapInfo mapInfo)
    {
        _mapInfo.OffsetX = mapInfo.OffsetX;
        _mapInfo.OffsetY = mapInfo.OffsetY;
        _mapInfo.CellSize = mapInfo.CellSize;
        _zoomBaseX = _dpiX/mapInfo.CellSize;
        _zoomBaseY = _dpiY/mapInfo.CellSize;
        UpdateVisibleArea();
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        // draw the _originalImage and overlay it with the _overlay
        //DeviceDpi 
        Graphics g = e.Graphics;
        _dpiX = DeviceDpi;
        _dpiY = DeviceDpi;
        _zoomBaseX = _dpiX/_mapInfo.CellSize;
        _zoomBaseY = _dpiY/_mapInfo.CellSize;
        
        
        Rectangle clipRect = e.ClipRectangle;
        g.SetClip(clipRect);
        var rectInOriginal = GetViewAreaInOriginal(); 
        
        g.ScaleTransform(ZoomX, ZoomY);
        g.TranslateTransform(_currentPanOffset.X, _currentPanOffset.Y);
        
        g.CompositingMode = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImage(_playersImage, rectInOriginal, rectInOriginal, GraphicsUnit.Pixel);
        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / ZoomY)))
        {
            for (float i = _mapInfo.OffsetX; i < rectInOriginal.X + rectInOriginal.Width; i += _mapInfo.CellSize)
                g.DrawLine(pen, i, rectInOriginal.Y, i, rectInOriginal.Y + rectInOriginal.Height);
        }

        using (var pen = new Pen(_gridColor, Math.Min(1.5f,3f / ZoomX)))
        {
            for (float i = _mapInfo.OffsetY; i < rectInOriginal.Y + rectInOriginal.Height; i += _mapInfo.CellSize)
                g.DrawLine(pen, rectInOriginal.X, i, rectInOriginal.X + rectInOriginal.Width, i);
        }
    }
}