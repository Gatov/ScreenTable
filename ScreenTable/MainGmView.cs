namespace ScreenTable;

public enum Mode
{
    None,
    Reveal,
    Calibrate,
    Pan
}

public class MapInfo
{
    public int OffsetX = 5;
    public int OffsetY = 5;
    public float CellSize = 48;
    public string filePath = "C:\\Users\\Public\\Pictures\\Sample Pictures\\";
}

public partial class MainGmView : Form
{
    private readonly GMControl _gmControl;
    private readonly ToolStripStatusLabel _toolStripStatusLabel;

    private Dictionary<Mode, string> _tips = new Dictionary<Mode, string>()
    {
        { Mode.None, "Drop Image for new map | Left Click to reveal | Wheel Click to pan | Wheel to zoom | Shift-Left Click to calibrate" },
        { Mode.Reveal, "Reveal" },
        { Mode.Calibrate, "Calibrate" },
        { Mode.Pan, "Pan" }
    };
    
    public MainGmView()
    {
        InitializeComponent();
        _gmControl = new GMControl();
        _gmControl.Dock = DockStyle.Fill;
        
        StatusStrip statusStrip = new StatusStrip();
        _toolStripStatusLabel = new ToolStripStatusLabel(_tips[Mode.None]);;
        _gmControl.OnModeChanged += mode =>
        {
            _toolStripStatusLabel.Text = _tips[mode];
        };
        statusStrip.Dock = DockStyle.Bottom;
        statusStrip.Items.Add(_toolStripStatusLabel);
        Controls.Add(statusStrip);
        Controls.Add(_gmControl);
    }
}