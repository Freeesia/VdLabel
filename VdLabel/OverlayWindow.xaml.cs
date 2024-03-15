using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using static PInvoke.User32;

namespace VdLabel;

/// <summary>
/// OverlayWindow.xaml の相互作用ロジック
/// </summary>
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TRANSPARENT);
    }
}

[ValueConversion(typeof(System.Drawing.Color), typeof(SolidColorBrush))]
public sealed class SystemColorToSolidBrushConverter : IValueConverter
{
    public static SystemColorToSolidBrushConverter Default { get; } = new SystemColorToSolidBrushConverter();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        System.Drawing.Color color = (System.Drawing.Color)value;
        System.Windows.Media.Color converted = Color.FromArgb(color.A, color.R, color.G, color.B);
        return new SolidColorBrush(converted);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        SolidColorBrush brush = (SolidColorBrush)value;
        System.Drawing.Color converted = System.Drawing.Color.FromArgb(brush.Color.A, brush.Color.R, brush.Color.G, brush.Color.B);
        return converted;
    }
}