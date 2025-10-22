using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static Windows.Win32.PInvoke;
using Wpf.Ui.Controls;
using Wpf.Ui.Tray.Controls;

namespace VdLabel;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly IContentDialogService contentDialogService;
    private readonly IVirualDesktopService virualDesktopService;
    private readonly IPresentationService presentationService;
    private readonly StartupOptions startupOptions;

    public MainWindow(IContentDialogService contentDialogService, IVirualDesktopService virualDesktopService, IPresentationService presentationService, StartupOptions startupOptions)
    {
        SystemThemeWatcher.Watch(this);
        InitializeComponent();
        this.contentDialogService = contentDialogService;
        this.virualDesktopService = virualDesktopService;
        this.presentationService = presentationService;
        this.startupOptions = startupOptions;
        this.contentDialogService.SetDialogHost(this.RootContentDialog);
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
    {
        if (this.startupOptions.IsAutoStarted)
        {
            this.Dispatcher.InvokeAsync(Hide);
        }
        var window = new WindowInteropHelper(this);
        for (var i = 0; i < 20; i++)
        {
            var mod = HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_CONTROL;
            mod |= i < 10 ? 0 : HOT_KEY_MODIFIERS.MOD_ALT;
            var key = (i < 10 ? i : i - 10) + Key.NumPad0;
            RegisterHotKey(new(window.Handle), i, mod, (uint)KeyInterop.VirtualKeyFromKey(key));
        }
        // Win10とWin11で使えるのショートカットが異なるので、バージョンで判定して登録するショートカットを切り替える
        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000, 0))
        {
            RegisterHotKey(new(window.Handle), 20, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_CONTROL, (uint)KeyInterop.VirtualKeyFromKey(Key.Up));
            RegisterHotKey(new(window.Handle), 21, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_CONTROL, (uint)KeyInterop.VirtualKeyFromKey(Key.Down));
        }
        else
        {
            RegisterHotKey(new(window.Handle), 20, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.Up));
            RegisterHotKey(new(window.Handle), 21, HOT_KEY_MODIFIERS.MOD_WIN | HOT_KEY_MODIFIERS.MOD_ALT, (uint)KeyInterop.VirtualKeyFromKey(Key.Down));
        }
        var source = HwndSource.FromHwnd(window.Handle);
        source.AddHook(WndProc);

    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY)
        {
            return 0;
        }
        var i = wParam.ToInt32();
        if (i < 20)
        {
            this.virualDesktopService.Swtich(i);
        }
        else if (i == 20)
        {
            this.virualDesktopService.PopupOverlay();
        }
        else if (i == 21)
        {
            this.presentationService.OpenWindowAsync<DesktopCatalogViewModel>();
        }
        return 0;
    }

    private void FluentWindow_Activated(object sender, EventArgs e)
        => this.Dispatcher.InvokeAsync(() =>
            this.virualDesktopService.Pin(this));

    private void NotifyIcon_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var notifyIcon = (NotifyIcon)sender;
        if (notifyIcon.Menu is not null)
        {
            notifyIcon.Menu.DataContext = e.NewValue;
        }
    }
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

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class FalseToVisibilityConverter : IValueConverter
{
    public static FalseToVisibilityConverter Default { get; } = new FalseToVisibilityConverter();

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is Visibility v && v != Visibility.Visible;
    }
}
