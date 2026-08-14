using System.IO;
using System.Windows.Media.Imaging;

namespace PixelPress.App.ViewModels;

public sealed class InputFileItemViewModel : ObservableObject
{
    private InputFileItemViewModel(string filePath, BitmapSource? thumbnail)
    {
        FilePath = filePath;
        Thumbnail = thumbnail;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public BitmapSource? Thumbnail { get; }

    public static InputFileItemViewModel Create(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new InputFileItemViewModel(filePath, LoadThumbnail(filePath));
    }

    private static BitmapSource? LoadThumbnail(string filePath)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 84;
            image.UriSource = new Uri(filePath);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
