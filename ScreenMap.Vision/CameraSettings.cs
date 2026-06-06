using System;
using System.IO;
using Newtonsoft.Json;

namespace ScreenMap.Vision;

public class CameraSettings
{
    public bool Enabled { get; set; } = false;
    public int DeviceIndex { get; set; } = 0;
    public double IntervalSeconds { get; set; } = 2.0;
    public bool ShowOnGmView { get; set; } = true;
    /// <summary>When true the detection overlay draws the isolated figurine photo at each
    /// detected location; when false it falls back to the plain green circle marker.</summary>
    public bool ShowFigurines { get; set; } = true;
    /// <summary>No-grid fallback min blob size, in pixels (used when the grid scale is unknown).</summary>
    public int MinBlobAreaPx { get; set; } = 800;
    /// <summary>Smallest object kept and drawn, in grid cells (1 cell = 2.5 cm). Used when the
    /// map grid scale is known; auto-adjust sets this to the placed token's size.</summary>
    public double MinObjectCells { get; set; } = 1.0;
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
