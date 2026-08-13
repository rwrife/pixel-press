using PixelPress.App.Services;
using PixelPress.Core;
using PixelPress.Core.Abstractions;
using PixelPress.Core.Operations;
using PixelPress.Core.Recipes;

namespace PixelPress.App.ViewModels;

public enum OperationKind
{
    Resize,
    Convert,
    Compress,
    Watermark,
    Rename
}

public sealed class OperationViewModel : ObservableObject
{
    private OperationKind _kind;
    private bool _isEnabled = true;
    private ResizeMode _resizeMode = ResizeMode.LongestEdge;
    private int _width = 1600;
    private int _height = 1200;
    private double _percentage = 100;
    private bool _allowUpscale;
    private OutputImageFormat _targetFormat = OutputImageFormat.Webp;
    private int _quality = 80;
    private bool _stripMetadata = true;
    private string _watermarkText = "pixel-press";
    private WatermarkPosition _watermarkPosition = WatermarkPosition.BottomRight;
    private double _watermarkOpacity = 0.35;
    private double _watermarkScale = 0.20;
    private string _renameTemplate = "{name}-{index}.{ext}";

    public OperationViewModel(OperationKind kind = OperationKind.Resize)
    {
        _kind = kind;
        ApplyKindDefaults(kind);
    }

    public static IReadOnlyList<OperationKind> AllKinds { get; } = Enum.GetValues<OperationKind>();

    public static IReadOnlyList<ResizeMode> ResizeModes { get; } = Enum.GetValues<ResizeMode>();

    public static IReadOnlyList<OutputImageFormat> OutputFormats { get; } = Enum.GetValues<OutputImageFormat>();

    public static IReadOnlyList<WatermarkPosition> WatermarkPositions { get; } = Enum.GetValues<WatermarkPosition>();

    public OperationKind Kind
    {
        get => _kind;
        set
        {
            if (SetProperty(ref _kind, value))
            {
                ApplyKindDefaults(value);
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public ResizeMode ResizeMode
    {
        get => _resizeMode;
        set => SetProperty(ref _resizeMode, value);
    }

    public int Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public int Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public double Percentage
    {
        get => _percentage;
        set => SetProperty(ref _percentage, value);
    }

    public bool AllowUpscale
    {
        get => _allowUpscale;
        set => SetProperty(ref _allowUpscale, value);
    }

    public OutputImageFormat TargetFormat
    {
        get => _targetFormat;
        set => SetProperty(ref _targetFormat, value);
    }

    public int Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    public bool StripMetadata
    {
        get => _stripMetadata;
        set => SetProperty(ref _stripMetadata, value);
    }

    public string WatermarkText
    {
        get => _watermarkText;
        set => SetProperty(ref _watermarkText, value);
    }

    public WatermarkPosition WatermarkPosition
    {
        get => _watermarkPosition;
        set => SetProperty(ref _watermarkPosition, value);
    }

    public double WatermarkOpacity
    {
        get => _watermarkOpacity;
        set => SetProperty(ref _watermarkOpacity, value);
    }

    public double WatermarkScale
    {
        get => _watermarkScale;
        set => SetProperty(ref _watermarkScale, value);
    }

    public string RenameTemplate
    {
        get => _renameTemplate;
        set => SetProperty(ref _renameTemplate, value);
    }

    public IImageOperation? BuildOperation()
    {
        if (!IsEnabled)
        {
            return null;
        }

        return Kind switch
        {
            OperationKind.Resize => BuildResizeOperation(),
            OperationKind.Convert => new ConvertOperation(TargetFormat),
            OperationKind.Compress => CompressOperation.QualityOnly(Math.Clamp(Quality, 0, 100), StripMetadata),
            OperationKind.Watermark => BuildWatermarkOperation(),
            OperationKind.Rename => BuildRenameOperation(),
            _ => null
        };
    }

    public OperationSettings ToSettings()
    {
        return new OperationSettings
        {
            Kind = Kind,
            IsEnabled = IsEnabled,
            ResizeMode = ResizeMode,
            Width = Width,
            Height = Height,
            Percentage = Percentage,
            AllowUpscale = AllowUpscale,
            TargetFormat = TargetFormat,
            Quality = Quality,
            StripMetadata = StripMetadata,
            WatermarkText = WatermarkText,
            WatermarkPosition = WatermarkPosition,
            WatermarkOpacity = WatermarkOpacity,
            WatermarkScale = WatermarkScale,
            RenameTemplate = RenameTemplate
        };
    }

    public static OperationViewModel FromSettings(OperationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new OperationViewModel(settings.Kind)
        {
            IsEnabled = settings.IsEnabled,
            ResizeMode = settings.ResizeMode,
            Width = settings.Width,
            Height = settings.Height,
            Percentage = settings.Percentage,
            AllowUpscale = settings.AllowUpscale,
            TargetFormat = settings.TargetFormat,
            Quality = settings.Quality,
            StripMetadata = settings.StripMetadata,
            WatermarkText = settings.WatermarkText,
            WatermarkPosition = settings.WatermarkPosition,
            WatermarkOpacity = settings.WatermarkOpacity,
            WatermarkScale = settings.WatermarkScale,
            RenameTemplate = settings.RenameTemplate
        };
    }

    public RecipeOperation ToRecipeOperation()
    {
        return new RecipeOperation
        {
            Kind = Kind switch
            {
                OperationKind.Resize => RecipeOperationKind.Resize,
                OperationKind.Convert => RecipeOperationKind.Convert,
                OperationKind.Compress => RecipeOperationKind.Compress,
                OperationKind.Watermark => RecipeOperationKind.Watermark,
                OperationKind.Rename => RecipeOperationKind.Rename,
                _ => RecipeOperationKind.Resize
            },
            IsEnabled = IsEnabled,
            ResizeMode = ResizeMode,
            Width = Width,
            Height = Height,
            Percentage = Percentage,
            AllowUpscale = AllowUpscale,
            TargetFormat = TargetFormat,
            Quality = Quality,
            StripMetadata = StripMetadata,
            WatermarkText = WatermarkText,
            WatermarkPosition = WatermarkPosition,
            WatermarkOpacity = WatermarkOpacity,
            WatermarkScale = WatermarkScale,
            RenameTemplate = RenameTemplate
        };
    }

    public static OperationViewModel FromRecipeOperation(RecipeOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var kind = operation.Kind switch
        {
            RecipeOperationKind.Resize => OperationKind.Resize,
            RecipeOperationKind.Convert => OperationKind.Convert,
            RecipeOperationKind.Compress => OperationKind.Compress,
            RecipeOperationKind.Watermark => OperationKind.Watermark,
            RecipeOperationKind.Rename => OperationKind.Rename,
            _ => OperationKind.Resize
        };

        return new OperationViewModel(kind)
        {
            IsEnabled = operation.IsEnabled,
            ResizeMode = operation.ResizeMode,
            Width = operation.Width,
            Height = operation.Height,
            Percentage = operation.Percentage,
            AllowUpscale = operation.AllowUpscale,
            TargetFormat = operation.TargetFormat,
            Quality = operation.Quality,
            StripMetadata = operation.StripMetadata,
            WatermarkText = operation.WatermarkText,
            WatermarkPosition = operation.WatermarkPosition,
            WatermarkOpacity = operation.WatermarkOpacity,
            WatermarkScale = operation.WatermarkScale,
            RenameTemplate = operation.RenameTemplate
        };
    }

    private IImageOperation BuildResizeOperation()
    {
        var width = Math.Max(1, Width);
        var height = Math.Max(1, Height);

        return ResizeMode switch
        {
            ResizeMode.LongestEdge => ResizeOperation.LongestEdge(width, AllowUpscale),
            ResizeMode.ExactWxH => ResizeOperation.Exact(width, height, AllowUpscale),
            ResizeMode.Percentage => ResizeOperation.Percentage(Math.Max(1, Percentage), AllowUpscale),
            ResizeMode.Fit => ResizeOperation.Fit(width, height, AllowUpscale),
            ResizeMode.Fill => ResizeOperation.Fill(width, height, AllowUpscale),
            _ => ResizeOperation.LongestEdge(width, AllowUpscale)
        };
    }

    private IImageOperation? BuildWatermarkOperation()
    {
        if (string.IsNullOrWhiteSpace(WatermarkText))
        {
            return null;
        }

        return WatermarkOperation.TextOverlay(
            WatermarkText,
            WatermarkPosition,
            opacity: (float)Math.Clamp(WatermarkOpacity, 0.0, 1.0),
            relativeScale: (float)Math.Clamp(WatermarkScale, 0.02, 1.0),
            marginPixels: 16);
    }

    private IImageOperation? BuildRenameOperation()
    {
        if (string.IsNullOrWhiteSpace(RenameTemplate))
        {
            return null;
        }

        return new OutputNamingOperation(RenameTemplate);
    }

    private void ApplyKindDefaults(OperationKind kind)
    {
        switch (kind)
        {
            case OperationKind.Resize:
                Width = Width <= 0 ? 1600 : Width;
                Height = Height <= 0 ? 1200 : Height;
                Percentage = Percentage <= 0 ? 100 : Percentage;
                break;
            case OperationKind.Convert:
                TargetFormat = TargetFormat;
                break;
            case OperationKind.Compress:
                Quality = Quality is < 0 or > 100 ? 80 : Quality;
                break;
            case OperationKind.Watermark:
                if (string.IsNullOrWhiteSpace(WatermarkText))
                {
                    WatermarkText = "pixel-press";
                }

                WatermarkOpacity = Math.Clamp(WatermarkOpacity, 0.0, 1.0);
                WatermarkScale = Math.Clamp(WatermarkScale, 0.02, 1.0);
                break;
            case OperationKind.Rename:
                if (string.IsNullOrWhiteSpace(RenameTemplate))
                {
                    RenameTemplate = "{name}-{index}.{ext}";
                }

                break;
        }
    }
}
