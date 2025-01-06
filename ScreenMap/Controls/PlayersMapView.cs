using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ScreenMap.Logic;
// ReSharper disable LocalizableElement

namespace ScreenMap.Controls;

public partial class PlayersMapView : UserControl
{
    private PlayersMap _map;

    public PlayersMapView()
    {
        InitializeComponent();
        DoubleBuffered = true;
        Paint+= OnPaint;
        Load += OnLoad;
        SizeChanged += (sender, args) => UpdateVisibleArea();
    }
    
    public void SetMap(PlayersMap map)
    {
        _map = map;
        var form = FindForm();
        if (form != null)
            form.Text = $"Players:{map.Name}";
        
        _map.OnRectUpdated += rc =>
        {
            SafeUpdate(rc);
        };
        
        DoubleBuffered = true;
    }

    private void SafeUpdate(RectangleF rc)
    {
        if (InvokeRequired)
            Invoke(() => SafeUpdate(rc));
        else
        {
            if(rc.IsEmpty)
                Invalidate();
            else
                Invalidate(Rectangle.Ceiling(rc));
        }
    }
    private void OnPaint(object sender, PaintEventArgs e)
    {
        if (_map != null)
        {
            _map.OnPaint(e.Graphics, ClientSize);
        }
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
    { // no background painting
    }
    
    private void UpdateVisibleArea()
    {
        if (_map != null)
        {
            _map.UpdateVisibleArea(ClientSize.Width, ClientSize.Height);
            Invalidate();
        }
    }
}