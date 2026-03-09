using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FocusShade;

public enum ShakeSensitivity
{
    Off,
    Low,
    Medium,
    High
}

public class SettingsModel
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FocusShade");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool IsEnabled { get; set; }
    public int BlurIntensity { get; set; } = 50;
    public int DimIntensity { get; set; } = 50;
    public ShakeSensitivity ShakeSensitivity { get; set; } = ShakeSensitivity.Medium;
    public uint HotkeyModifiers { get; set; } = NativeMethods.MOD_CONTROL | NativeMethods.MOD_SHIFT;
    public uint HotkeyKey { get; set; } = 0x46; // VK_F
    public bool StartWithWindows { get; set; }

    [JsonIgnore]
    public int BlurRadiusPx => (int)Math.Round(BlurIntensity * 20.0 / 100.0);

    [JsonIgnore]
    public byte DimAlpha => (byte)Math.Clamp((int)Math.Round(DimIntensity * 180.0 / 100.0), 0, 255);

    [JsonIgnore]
    public int ShakeDistanceThresholdPx => ShakeSensitivity switch
    {
        ShakeSensitivity.Off => int.MaxValue,
        ShakeSensitivity.Low => 700,
        ShakeSensitivity.Medium => 500,
        ShakeSensitivity.High => 300,
        _ => 500
    };

    public static SettingsModel Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<SettingsModel>(json);
                if (loaded != null)
                    return loaded;
            }
        }
        catch
        {
            // Use defaults on any error
        }

        return new SettingsModel();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonStringEnumConverter() }
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
