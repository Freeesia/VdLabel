using PInvoke;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using static PInvoke.User32;

namespace VdLabel;

/// <summary>
/// OverlayWindow.xaml の相互作用ロジック
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer timer;

    public OverlayWindow(App app)
    {
        this.timer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, OnTick, app.Dispatcher);
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TopMost();
        await Task.Delay(Random.Shared.Next(100, 2000));
        this.timer.Start();
    }
    private void OnTick(object? sender, EventArgs e)
        => TopMost();

    protected override void OnClosed(EventArgs e)
    {
        this.timer.Stop();
        base.OnClosed(e);
    }


    private void TopMost()
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TRANSPARENT);

        // ShowInTaskbarをfalseにすると↓の方法で一番上に表示する必要がある
        // https://social.msdn.microsoft.com/Forums/en-US/cdbe457f-d653-4a18-9295-bb9b609bc4e3/desktop-apps-on-top-of-metro-extended
        IntPtr hWndHiddenOwner = User32.GetWindow(windowHandle, GetWindowCommands.GW_OWNER);
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
        // 2回呼ばないと安定して最上位にならない
        SetWindowPos(hWndHiddenOwner, new(-1), 0, 0, 0, 0, SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
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

[ValueConversion(typeof(System.Drawing.Color), typeof(SolidColorBrush))]
public sealed class BadgeColorToForegroundConverter : IValueConverter
{
    public static BadgeColorToForegroundConverter Default { get; } = new BadgeColorToForegroundConverter();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is System.Drawing.Color bgColor)
        {
            float h = bgColor.GetHue();
            float s = bgColor.GetSaturation();
            float brightness = bgColor.GetBrightness();
            // Keep the hue and saturation, flip the lightness for contrast
            float textBrightness = brightness > 0.5f ? 0.15f : 0.85f;
            var textColor = HslToColor(h, s, textBrightness);
            return new SolidColorBrush(textColor);
        }
        return new SolidColorBrush(System.Windows.Media.Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();

    private static System.Windows.Media.Color HslToColor(float hDegrees, float s, float l)
    {
        double r, g, b;
        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            double p = 2 * l - q;
            double h = hDegrees / 360.0;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }
        return System.Windows.Media.Color.FromArgb(255, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }
}