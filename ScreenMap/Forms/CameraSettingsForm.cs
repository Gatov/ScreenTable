using System;
using System.Drawing;
using System.Windows.Forms;
using ScreenMap.Vision;

namespace ScreenMap.Forms;

public class CameraSettingsForm : Form
{
    private readonly CameraSettings _settings;
    private readonly ComboBox _deviceCombo;
    private readonly TrackBar _intervalSlider;
    private readonly Label _intervalLabel;
    private readonly TrackBar _thresholdSlider;
    private readonly Label _thresholdLabel;
    private readonly TrackBar _minSizeSlider;
    private readonly Label _minSizeLabel;
    private readonly TrackBar _distortionSlider;
    private readonly Label _distortionLabel;
    private readonly CheckBox _enabledCheck;
    private readonly CheckBox _showGmCheck;
    private readonly CheckBox _showFiguresCheck;

    public CameraSettings Result => _settings;

    public CameraSettingsForm(CameraSettings settings)
    {
        _settings = settings;
        Text = "Camera Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9F);
        // Let WinForms size everything from content so the layout survives any DPI
        // scaling instead of clipping under hard-coded pixel coordinates.
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // Width the sliders / combo should fill. Drives the dialog's overall width.
        const int contentWidth = 440;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(14),
        };
        Controls.Add(root);

        // --- Camera device --------------------------------------------------
        var deviceGroup = NewGroup("Camera");
        var deviceLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8, 4, 8, 8),
        };
        deviceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        deviceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        deviceLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        deviceLayout.Controls.Add(new Label
        {
            Text = "Device:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
        }, 0, 0);
        _deviceCombo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownWidth = 360,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        deviceLayout.Controls.Add(_deviceCombo, 1, 0);
        var rescanBtn = new Button
        {
            Text = "Rescan",
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0),
        };
        rescanBtn.Click += (_, _) => PopulateDevices();
        deviceLayout.Controls.Add(rescanBtn, 2, 0);
        deviceGroup.Controls.Add(deviceLayout);
        root.Controls.Add(deviceGroup);

        // --- Detection tuning ----------------------------------------------
        var detectGroup = NewGroup("Detection");
        var detectLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(8, 4, 8, 8),
        };

        _intervalLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _intervalSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(contentWidth, 0),
            Minimum = 10,
            Maximum = 50,
            TickFrequency = 5,
            Value = Math.Clamp((int)Math.Round(settings.IntervalSeconds * 10), 10, 50)
        };
        _intervalSlider.ValueChanged += (_, _) => UpdateIntervalLabel();
        detectLayout.Controls.Add(_intervalLabel);
        detectLayout.Controls.Add(_intervalSlider);
        UpdateIntervalLabel();

        // Sensitivity = how different a patch must be from the map to count as an object.
        _thresholdLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _thresholdSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(contentWidth, 0),
            Minimum = 20,
            Maximum = 150,
            TickFrequency = 10,
            Value = Math.Clamp(settings.DiffThreshold, 20, 150)
        };
        _thresholdSlider.ValueChanged += (_, _) => UpdateThresholdLabel();
        detectLayout.Controls.Add(_thresholdLabel);
        detectLayout.Controls.Add(_thresholdSlider);
        UpdateThresholdLabel();

        // Smallest object that counts, in grid cells (one cell = one 2.5 cm token).
        _minSizeLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _minSizeSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(contentWidth, 0),
            Minimum = 1,
            Maximum = 6,
            SmallChange = 1,
            LargeChange = 1,
            TickFrequency = 1,
            Value = Math.Clamp((int)Math.Round(settings.MinObjectCells), 1, 6)
        };
        _minSizeSlider.ValueChanged += (_, _) => UpdateMinSizeLabel();
        detectLayout.Controls.Add(_minSizeLabel);
        detectLayout.Controls.Add(_minSizeSlider);
        UpdateMinSizeLabel();

        // Lens fisheye/barrel correction (OpenCV k1). The TrackBar is integer-only, so units
        // 0..40 map to k1 = value / 200.0 — a 0.005 step over 0.000..0.200.
        _distortionLabel = new Label { AutoSize = true, Margin = new Padding(0, 6, 0, 0) };
        _distortionSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(contentWidth, 0),
            Minimum = 0,
            Maximum = 40,
            SmallChange = 1,
            LargeChange = 5,
            TickFrequency = 5,
            Value = Math.Clamp((int)Math.Round(settings.LensDistortionK1 * 200), 0, 40)
        };
        _distortionSlider.ValueChanged += (_, _) => UpdateDistortionLabel();
        detectLayout.Controls.Add(_distortionLabel);
        detectLayout.Controls.Add(_distortionSlider);
        UpdateDistortionLabel();

        detectGroup.Controls.Add(detectLayout);
        root.Controls.Add(detectGroup);

        // --- Display options ------------------------------------------------
        var displayGroup = NewGroup("Display");
        var displayLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8, 4, 8, 8),
        };
        _enabledCheck = new CheckBox
        {
            Text = "Detection enabled",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4),
            Checked = settings.Enabled
        };
        _showGmCheck = new CheckBox
        {
            Text = "Show detections on GM view",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4),
            Checked = settings.ShowOnGmView
        };
        _showFiguresCheck = new CheckBox
        {
            Text = "Show figurine images (off = green circles)",
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 4),
            Checked = settings.ShowFigurines
        };
        displayLayout.Controls.Add(_enabledCheck);
        displayLayout.Controls.Add(_showGmCheck);
        displayLayout.Controls.Add(_showFiguresCheck);
        displayGroup.Controls.Add(displayLayout);
        root.Controls.Add(displayGroup);

        // --- Buttons --------------------------------------------------------
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 6, 0, 0),
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(80, 0),
            Margin = new Padding(6, 0, 0, 0),
        };
        var okBtn = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(80, 0),
            Margin = new Padding(6, 0, 0, 0),
        };
        buttons.Controls.Add(cancelBtn);
        buttons.Controls.Add(okBtn);
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
        root.Controls.Add(buttons);

        okBtn.Click += (_, _) =>
        {
            _settings.DeviceIndex = _deviceCombo.SelectedItem is DeviceItem item ? item.Index : 0;
            _settings.IntervalSeconds = _intervalSlider.Value / 10.0;
            _settings.DiffThreshold = _thresholdSlider.Value;
            _settings.MinObjectCells = _minSizeSlider.Value;
            _settings.LensDistortionK1 = _distortionSlider.Value / 200.0;
            _settings.Enabled = _enabledCheck.Checked;
            _settings.ShowOnGmView = _showGmCheck.Checked;
            _settings.ShowFigurines = _showFiguresCheck.Checked;
            _settings.Save();
        };

        PopulateDevices();
    }

    private static GroupBox NewGroup(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0, 0, 0, 10),
        Padding = new Padding(6, 4, 6, 6),
    };

    private void UpdateIntervalLabel()
    {
        _intervalLabel.Text = $"Detection interval: {_intervalSlider.Value / 10.0:0.0} s";
    }

    private void UpdateThresholdLabel()
    {
        _thresholdLabel.Text = $"Sensitivity threshold: {_thresholdSlider.Value}  (lower = more sensitive)";
    }

    private void UpdateMinSizeLabel()
    {
        int cells = _minSizeSlider.Value;
        _minSizeLabel.Text = $"Min object size: {cells} cell{(cells == 1 ? "" : "s")} (one cell = 2.5 cm)";
    }

    private void UpdateDistortionLabel()
    {
        _distortionLabel.Text = $"Lens distortion: {_distortionSlider.Value / 200.0:0.000}  (0 = none)";
    }

    private void PopulateDevices()
    {
        _deviceCombo.Items.Clear();
        // Always include the configured device: a device currently in use (e.g. by an
        // open preview) fails the exclusive DSHOW probe and would otherwise vanish.
        var indices = new System.Collections.Generic.SortedSet<int>(OpenCvCameraSource.EnumerateDevices())
        {
            _settings.DeviceIndex
        };
        // Friendly names are positional: DirectShow index i == OpenCV DSHOW index i.
        var names = DirectShowDevices.GetNames();
        int selected = 0;
        foreach (var i in indices)
        {
            var name = i >= 0 && i < names.Count ? names[i] : "";
            var added = _deviceCombo.Items.Add(new DeviceItem(i, name));
            if (i == _settings.DeviceIndex) selected = added;
        }
        if (_deviceCombo.Items.Count > 0) _deviceCombo.SelectedIndex = selected;
    }

    private readonly record struct DeviceItem(int Index, string Name)
    {
        public override string ToString() =>
            string.IsNullOrEmpty(Name) ? $"Camera {Index}" : $"Camera {Index}: {Name}";
    }
}
