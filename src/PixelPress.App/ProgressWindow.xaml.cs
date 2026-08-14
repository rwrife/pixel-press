using System.IO;
using System.Windows;
using PixelPress.Core;

namespace PixelPress.App;

public partial class ProgressWindow : Window
{
    public ProgressWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? CancelRequested;

    public void UpdateProgress(PipelineProgress progress)
    {
        RunProgressBar.Maximum = Math.Max(1, progress.Total);
        RunProgressBar.Value = Math.Clamp(progress.Completed, 0, progress.Total);

        CountText.Text = $"{progress.Completed} / {progress.Total}";

        var fileName = Path.GetFileName(progress.InputPath);
        StatusText.Text = $"{progress.Status}: {fileName}";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
