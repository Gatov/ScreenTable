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
        ClientSize = new Size(360, 396);
        MinimizeBox = false;
        MaximizeBox = false;

        int y = 12;
        Controls.Add(new Label { Text = "Device:", Location = new Point(12, y + 3), AutoSize = true });
        _deviceCombo = new ComboBox
        {
            Location = new Point(110, y),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        Controls.Add(_deviceCombo);
        var rescanBtn = new Button { Text = "Rescan", Location = new Point(276, y - 1), Width = 70 };
        rescanBtn.Click += (_, _) => PopulateDevices();
        Controls.Add(rescanBtn);

        y += 36;
        _intervalLabel = new Label { Location = new Point(12, y + 3), AutoSize = true };
        Controls.Add(_intervalLabel);
        _intervalSlider = new TrackBar
        {
            Location = new Point(12, y + 22),
            Width = 334,
            Minimum = 10,
            Maximum = 50,
            TickFrequency = 5,
            Value = Math.Clamp((int)Math.Round(settings.IntervalSeconds * 10), 10, 50)
        };
        _intervalSlider.ValueChanged += (_, _) => UpdateIntervalLabel();
        Controls.Add(_intervalSlider);
        UpdateIntervalLabel();

        // Sensitivity = how different a patch must be from the map to count as an object.
        y += 70;
        _thresholdLabel = new Label { Location = new Point(12, y + 3), AutoSize = true };
        Controls.Add(_thresholdLabel);
        _thresholdSlider = new TrackBar
        {
            Location = new Point(12, y + 22),
            Width = 334,
            Minimum = 20,
            Maximum = 150,
            TickFrequency = 10,
            Value = Math.Clamp(settings.DiffThreshold, 20, 150)
        };
        _thresholdSlider.ValueChanged += (_, _) => UpdateThresholdLabel();
        Controls.Add(_thresholdSlider);
        UpdateThresholdLabel();

        // Smallest object that counts, in grid cells (one cell = one 2.5 cm token).
        y += 70;
        _minSizeLabel = new Label { Location = new Point(12, y + 3), AutoSize = true };
        Controls.Add(_minSizeLabel);
        _minSizeSlider = new TrackBar
        {
            Location = new Point(12, y + 22),
            Width = 334,
            Minimum = 1,
            Maximum = 6,
            SmallChange = 1,
            LargeChange = 1,
            TickFrequency = 1,
            Value = Math.Clamp((int)Math.Round(settings.MinObjectCells), 1, 6)
        };
        _minSizeSlider.ValueChanged += (_, _) => UpdateMinSizeLabel();
        Controls.Add(_minSizeSlider);
        UpdateMinSizeLabel();

        y += 70;
        _enabledCheck = new CheckBox
        {
            Text = "Detection enabled",
            Location = new Point(12, y),
            AutoSize = true,
            Checked = settings.Enabled
        };
        Controls.Add(_enabledCheck);

        y += 24;
        _showGmCheck = new CheckBox
        {
            Text = "Show detections on GM view",
            Location = new Point(12, y),
            AutoSize = true,
            Checked = settings.ShowOnGmView
        };
        Controls.Add(_showGmCheck);

        y += 24;
        _showFiguresCheck = new CheckBox
        {
            Text = "Show figurine images (off = green circles)",
            Location = new Point(12, y),
            AutoSize = true,
            Checked = settings.ShowFigurines
        };
        Controls.Add(_showFiguresCheck);

        var okBtn = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(184, 358),
            Width = 80
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(270, 358),
            Width = 80
        };
        AcceptButton = okBtn;
        CancelButton = cancelBtn;
        Controls.Add(okBtn);
        Controls.Add(cancelBtn);

        okBtn.Click += (_, _) =>
        {
            _settings.DeviceIndex = _deviceCombo.SelectedItem is int di ? di : 0;
            _settings.IntervalSeconds = _intervalSlider.Value / 10.0;
            _settings.DiffThreshold = _thresholdSlider.Value;
            _settings.MinObjectCells = _minSizeSlider.Value;
            _settings.Enabled = _enabledCheck.Checked;
            _settings.ShowOnGmView = _showGmCheck.Checked;
            _settings.ShowFigurines = _showFiguresCheck.Checked;
            _settings.Save();
        };

        PopulateDevices();
    }

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

    private void PopulateDevices()
    {
        _deviceCombo.Items.Clear();
        // Always include the configured device: a device currently in use (e.g. by an
        // open preview) fails the exclusive DSHOW probe and would otherwise vanish.
        var indices = new System.Collections.Generic.SortedSet<int>(OpenCvCameraSource.EnumerateDevices())
        {
            _settings.DeviceIndex
        };
        foreach (var i in indices) _deviceCombo.Items.Add(i);
        var idx = _deviceCombo.Items.IndexOf(_settings.DeviceIndex);
        _deviceCombo.SelectedIndex = idx >= 0 ? idx : 0;
    }
}
