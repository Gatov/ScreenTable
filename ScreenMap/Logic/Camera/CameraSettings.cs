using System;
using System.IO;
using Newtonsoft.Json;

namespace ScreenMap.Logic.Camera;

public class CameraSettings
{
    public bool Enabled { get; set; } = false;
    public int DeviceIndex { get; set; } = 0;
    public double IntervalSeconds { get; set; } = 2.0;
    public bool ShowOnGmView { get; set; } = true;
    public int MinBlobAreaPx { get; set; } = 800;
    public int DiffThreshold { get; set; } = 70;

    private static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "CameraSettings.json");

    public static CameraSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonConvert.DeserializeObject<CameraSettings>(File.ReadAllText(SettingsPath))
                       ?? new CameraSettings();
        }
        catch { }
        return new CameraSettings();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
        catch { }
    }
}
