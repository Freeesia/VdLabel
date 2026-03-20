using PInvoke;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wacton.Unicolour;
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
            // Use OKLCH luminance (L channel) via Unicolour to decide dark/light text
            var uni = new Unicolour(ColourSpace.Rgb255, bgColor.R, bgColor.G, bgColor.B);
            double luminance = uni.Oklch.L;
            // Keep hue/chroma, flip lightness for contrast
            double textLuminance = luminance > 0.5 ? 0.15 : 0.85;
            var textUni = new Unicolour(ColourSpace.Oklch, textLuminance, uni.Oklch.C, uni.Oklch.H);
            var rgb = textUni.Rgb;
            var textColor = Color.FromArgb(255,
                ClampToByte(rgb.Triplet.First * 255),
                ClampToByte(rgb.Triplet.Second * 255),
                ClampToByte(rgb.Triplet.Third * 255));
            return new SolidColorBrush(textColor);
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();

    private static byte ClampToByte(double value) => (byte)Math.Clamp(value, 0, 255);
}