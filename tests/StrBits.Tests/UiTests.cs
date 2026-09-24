using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StrBits.Core;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(StrBits.Tests.TestAppBuilder))]

namespace StrBits.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class UiTests
{
    [AvaloniaFact]
    public async Task EditingInputUpdatesOutputAndClipboard()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            var input = window.FindControl<TextBox>("InputBox")!;
            input.Focus();
            input.SelectAll();
            window.KeyTextInput("A");
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("A", window.ViewModel.Input);
            Assert.Equal("01000001", FindNamed<TextBox>(window, "Binary output").Text);
            var copy = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Copy output"));
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("01000001", await window.Clipboard!.TryGetTextAsync());
            Assert.Contains("copied", window.ViewModel.Status);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SelectingTypeExampleAndByteOrderUpdatesBindings()
    {
        var window = new MainWindow();
        try
        {
            window.Show();
            FindNamed<ComboBox>(window, "Data type").SelectedItem = BinaryConverter.Types.Single(t => t.Kind == DataKind.Int32);
            var example = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Use example"));
            example.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            FindNamed<ComboBox>(window, "Byte order").SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("42", window.FindControl<TextBox>("InputBox")!.Text);
            Assert.Equal("0000002A", window.ViewModel.Result!.ToHex());
            Assert.False(FindNamed<ComboBox>(window, "Text encoding").IsEnabled);
            var copy = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Copy output"));
            window.FindControl<TextBox>("InputBox")!.Text = "invalid";
            Dispatcher.UIThread.RunJobs();
            Assert.False(copy.IsEnabled);
            Assert.Empty(FindNamed<TextBox>(window, "Binary output").Text!);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MainWindowRendersAtDefaultAndMinimumSizes()
    {
        var app = Application.Current!;
        var originalTheme = app.RequestedThemeVariant;
        var window = new MainWindow();
        try
        {
            Assert.Equal(ThemeVariant.Default, originalTheme);
            window.Show();
            Color[]? previousColors = null;
            foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark, ThemeVariant.Light })
            {
                app.RequestedThemeVariant = theme;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(theme, window.ActualThemeVariant);
                var histogram = window.GetVisualDescendants().OfType<ByteHistogram>().Single();
                var input = window.FindControl<TextBox>("InputBox")!;
                var binary = FindNamed<TextBox>(window, "Binary output");
                var colors = new[] { window.Background, window.Foreground, input.Background,
                    binary.Foreground, histogram.BarBrush, histogram.GridBrush }
                    .Select(brush => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color).ToArray();
                if (previousColors is not null)
                    for (var i = 0; i < colors.Length; i++) Assert.NotEqual(previousColors[i], colors[i]);
                previousColors = colors;

                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                Assert.True(frame.PixelSize.Width >= 1000);
                var screenshot = Environment.GetEnvironmentVariable("STRBITS_SCREENSHOT_PATH");
                if (!string.IsNullOrEmpty(screenshot))
                {
                    var themedPath = Path.Combine(Path.GetDirectoryName(screenshot)!,
                        $"{Path.GetFileNameWithoutExtension(screenshot)}-{theme.Key}{Path.GetExtension(screenshot)}");
                    frame.Save(themedPath, PngBitmapEncoderOptions.Default);
                }
            }
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            Dispatcher.UIThread.RunJobs();
            Assert.True(window.FindControl<TextBox>("InputBox")!.Bounds.Width > 200);
            Assert.True(FindNamed<TextBox>(window, "Binary output").Bounds.Width > 300);
        }
        finally
        {
            window.Close();
            app.RequestedThemeVariant = originalTheme;
        }
    }

    private static T FindNamed<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetName(c) == name);
}
