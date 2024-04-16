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