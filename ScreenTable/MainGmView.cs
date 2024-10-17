namespace ScreenTable;

public enum Mode
{
    None,
    Reveal,
    Calibrate,
    Pan
}

public partial class MainGmView : Form
{
    private readonly GMControl _gmControl;

    private readonly Dictionary<Mode, string> _tips = new Dictionary<Mode, string>()
    {
        { Mode.None, "Drop Image for new map . Left Click to reveal . Middle Click to pan . Wheel to zoom . 'Calibrate' to calibrate grid" },
        { Mode.Reveal, "Paint to Reveal" },
        { Mode.Calibrate, "Calibrate, drag with Shift+Left Mouse button, then use Mouse wheel to adjust cells" },
        { Mode.Pan, "Pan" }
    };
    
    public MainGmView()
    {
        InitializeComponent();
        _gmControl = new GMControl();
        _gmControl.Dock = DockStyle.Fill;
        
        StatusStrip statusStrip = new StatusStrip();
        var toolStripStatusLabel = new ToolStripStatusLabel(_tips[Mode.None]);;
        var calibrate = new ToolStripButton("Calibrate");

        calibrate.Click += (sender, args) =>
        {
            calibrate.Checked = !calibrate.Checked;
            _gmControl.Calibration(calibrate.Checked);
        };
        calibrate.Alignment = ToolStripItemAlignment.Left;
       
        
        _gmControl.OnModeChanged += mode =>
        {
            toolStripStatusLabel.Text = _tips[mode];
        };
        toolStripStatusLabel.Alignment = ToolStripItemAlignment.Right;
        
        statusStrip.Dock = DockStyle.Bottom;
        statusStrip.Items.Add(calibrate);
        statusStrip.Items.Add(new ToolStripSeparator());
        statusStrip.Items.Add(toolStripStatusLabel);
        
        
        
        Controls.Add(statusStrip);
        Controls.Add(_gmControl);
    }
}