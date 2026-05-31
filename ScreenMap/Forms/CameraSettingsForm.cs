using System;
using System.Drawing;
using System.Windows.Forms;
using ScreenMap.Logic.Camera;

namespace ScreenMap.Forms;

public class CameraSettingsForm : Form
{
    private readonly CameraSettings _settings;
    private readonly ComboBox _deviceCombo;
    private readonly TrackBar _intervalSlider;
    private readonly Label _intervalLabel;
    private readonly CheckBox _enabledCheck;
    private readonly CheckBox _showGmCheck;

    public CameraSettings Result => _settings;

    public CameraSettingsForm(CameraSettings settings)
    {
        _settings = settings;
        Text = "Camera Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(360, 260);
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

        y += 80;
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


        var okBtn = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(184, 222),
            Width = 80
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(270, 222),
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
            _settings.Enabled = _enabledCheck.Checked;
            _settings.ShowOnGmView = _showGmCheck.Checked;
            _settings.Save();
        };

        PopulateDevices();
    }

    private void UpdateIntervalLabel()
    {
        _intervalLabel.Text = $"Detection interval: {_intervalSlider.Value / 10.0:0.0} s";
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
