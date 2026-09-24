using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace StrBits;

public partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void LoadExample(object? sender, RoutedEventArgs e) => ViewModel.LoadExample();

    private async void CopyOutput(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid) return;
        try
        {
            var clipboard = GetTopLevel(this)?.Clipboard;
            if (clipboard is null) { ViewModel.Status = "Clipboard is unavailable on this platform."; return; }
            await clipboard.SetTextAsync(ViewModel.GetCopyText());
            ViewModel.Status = "Output copied";
        }
        catch (Exception ex)
        {
            ViewModel.Status = $"Could not copy: {ex.Message}";
        }
    }

    private async void ExportReport(object? sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsValid) return;
        var report = ViewModel.CreateReport();
        try
        {
            if (!StorageProvider.CanSave) { ViewModel.Status = "Saving files is unavailable on this platform."; return; }
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export binary inspection",
                SuggestedFileName = "strbits-report.txt",
                DefaultExtension = "txt",
                FileTypeChoices = [new FilePickerFileType("Text report") { Patterns = ["*.txt"] }]
            });
            if (file is null) return;
            using (file)
            {
                await using var stream = await file.OpenWriteAsync();
                if (stream.CanSeek) stream.SetLength(0);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(report);
            }
            ViewModel.Status = "Report exported";
        }
        catch (Exception ex)
        {
            ViewModel.Status = $"Could not export: {ex.Message}";
        }
    }
}
