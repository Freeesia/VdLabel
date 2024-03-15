using System.Windows;
using System.Windows.Data;
using Wpf.Ui;
using Wpf.Ui.Appearance;

namespace VdLabel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow
{
    private readonly IContentDialogService contentDialogService;

    public MainWindow(IContentDialogService contentDialogService)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.contentDialogService = contentDialogService;
        this.contentDialogService.SetContentPresenter(this.RootContentDialog);
    }

    private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        Focus();
    }

    private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        => Application.Current.Shutdown();

    private void FluentWindow_Loaded(object sender, RoutedEventArgs e)
        => this.Dispatcher.InvokeAsync(Hide);
}

[ValueConversion(typeof(System.Drawing.Color), typeof(System.Windows.Media.Color))]
public sealed class SystemColorToMediaColorConverter : IValueConverter
{
    public static SystemColorToMediaColorConverter Default { get; } = new SystemColorToMediaColorConverter();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        System.Drawing.Color color = (System.Drawing.Color)value;
        return System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        System.Windows.Media.Color color = (System.Windows.Media.Color)value;
        return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
