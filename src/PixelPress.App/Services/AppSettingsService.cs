using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PixelPress.App.ViewModels;
using PixelPress.Core;
using PixelPress.Core.Operations;

namespace PixelPress.App.Services;

public sealed class AppSettings
{
    public string? OutputDirectory { get; set; }

    public List<OperationSettings> Operations { get; set; } = [];
}

public sealed class OperationSettings
{
    public OperationKind Kind { get; set; } = OperationKind.Resize;

    public bool IsEnabled { get; set; } = true;

    public ResizeMode ResizeMode { get; set; } = ResizeMode.LongestEdge;

    public int Width { get; set; } = 1600;

    public int Height { get; set; } = 1200;

    public double Percentage { get; set; } = 100;

    public bool AllowUpscale { get; set; }

    public OutputImageFormat TargetFormat { get; set; } = OutputImageFormat.Webp;

    public int Quality { get; set; } = 80;

    public bool StripMetadata { get; set; } = true;

    public string WatermarkText { get; set; } = "pixel-press";

    public WatermarkPosition WatermarkPosition { get; set; } = WatermarkPosition.BottomRight;

    public double WatermarkOpacity { get; set; } = 0.35;

    public double WatermarkScale { get; set; } = 0.20;

    public string RenameTemplate { get; set; } = "{name}-{index}.{ext}";
}

public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var root = Path.Combine(appDataPath, "pixel-press");
        _settingsPath = Path.Combine(root, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_settingsPath) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }
}
