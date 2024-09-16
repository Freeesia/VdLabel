using System.Windows;
using System.Windows.Interop;
using Kamishibai;
using System.Windows.Data;
using System.Globalization;
using ObservableCollections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Forms;
using static PInvoke.User32;
using static VdLabel.ProcessUtility;
using CommunityToolkit.Mvvm.Input;

namespace VdLabel;
/// <summary>
/// TargetWindowOverlay.xaml の相互作用ロジック
/// </summary>
public partial class TargetWindowOverlay : Window
{
    private static readonly List<TargetWindowOverlay> windows = [];
    private readonly Screen screen;

    public TargetWindowOverlay()
        : this(Screen.PrimaryScreen ?? throw new InvalidOperationException("プライマリスクリーンがない…"))
    {
    }

    public TargetWindowOverlay(Screen screen)
    {
        InitializeComponent();
        this.screen = screen;
        var point = screen.Bounds.Location;
        point.Offset(1, 1);
        var mon = MonitorFromPoint(point, MonitorOptions.MONITOR_DEFAULTTONEAREST);
        GetMonitorInfo(mon, out var monInfo);
        var eDpiScale = GetDpiForSystem() / 96.0;
        var left = monInfo.Monitor.left;
        var top = monInfo.Monitor.top;
        var width = monInfo.Monitor.right - monInfo.Monitor.left;
        var height = monInfo.Monitor.bottom - monInfo.Monitor.top;

        this.SetCurrentValue(LeftProperty, left / eDpiScale);
        this.SetCurrentValue(TopProperty, top / eDpiScale);
        this.SetCurrentValue(WidthProperty, width / eDpiScale);
        this.SetCurrentValue(HeightProperty, height / eDpiScale);
        windows.Add(this);
    }


    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;
        var extendedStyle = (SetWindowLongFlags)GetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE);
        var r = SetWindowLong(windowHandle, WindowLongIndexFlags.GWL_EXSTYLE, extendedStyle | SetWindowLongFlags.WS_EX_TOOLWINDOW);
        if (this.screen == Screen.PrimaryScreen)
        {
            ((TargetWindowViewModel)this.DataContext).Dialog = this;
            foreach (var screen in Screen.AllScreens.Where(s => !s.Primary))
            {
                new TargetWindowOverlay(screen) { DataContext = this.DataContext }.Show();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        windows.Remove(this);
        if (windows is [var window, ..] && window.IsLoaded)
        {
            window.Close();
        }
    }
}

public partial class TargetWindowViewModel : ObservableObject
{
    private static readonly IVirtualDesktopManager DesktopManager = (IVirtualDesktopManager)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a"))!)!;

    public ObservableList<WindowInfo> Windows { get; } = [];
    public TargetWindowOverlay? Dialog { get; internal set; }

    [ObservableProperty]
    private WindowInfo? selectedWindow;

    public TargetWindowViewModel()
        => Task.Run(CheckWindows);

    public void CheckWindows()
    {
        var hWnd = GetTopWindow(0);
        var z = 0;
        do
        {
            // ウィンドウが表示されていない場合は無視
            if (!IsWindowVisible(hWnd))
            {
                continue;
            }

            // 触れなさそうなウィンドウ無視
            var extendedStyle = (SetWindowLongFlags)GetWindowLong(hWnd, WindowLongIndexFlags.GWL_EXSTYLE);
            if (extendedStyle.HasFlag(SetWindowLongFlags.WS_EX_TOOLWINDOW) || extendedStyle.HasFlag(SetWindowLongFlags.WS_DISABLED) || extendedStyle.HasFlag(SetWindowLongFlags.WS_EX_LAYERED | SetWindowLongFlags.WS_EX_TRANSPARENT))
            {
                continue;
            }

            _ = GetWindowThreadProcessId(hWnd, out var processId);
            // 自分自身のプロセスは無視
            if (processId == Environment.ProcessId)
            {
                continue;
            }

            // ウィンドウタイトルが取得できない場合はスキップ
            if (GetWindowText(hWnd) is not { } windowTitle)
            {
                continue;
            }

            // ウィンドウが所属するプロセスのパスが取得できない場合はスキップ
            if (GetProcessPath(processId) is not { } path)
            {
                continue;
            }

            // ウィンドウが所属するプロセスのコマンドラインが取得できない場合はスキップ
            if (GetCommandLine(processId) is not { } commandLine)
            {
                continue;
            }

            var windowInfo = WINDOWINFO.Create();
            if (!GetWindowInfo(hWnd, ref windowInfo))
            {
                continue;
            }

            var clientRect = windowInfo.rcClient;
            var windowRect = windowInfo.rcWindow;

            if (string.IsNullOrEmpty(windowTitle) && path.Equals(@"C:\Windows\Explorer.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!DesktopManager.IsWindowOnCurrentVirtualDesktop(hWnd))
            {
                continue;
            }

            var monitorHandle = MonitorFromWindow(hWnd, MonitorOptions.MONITOR_DEFAULTTONEAREST);
            GetMonitorInfo(monitorHandle, out var monitorInfo);
            var eDpiScale = GetDpiForSystem() / 96.0;

            var p = GetWindowPlacement(hWnd);

            var left = clientRect.left;
            var top = p.showCmd.HasFlag(WindowShowStyle.SW_MAXIMIZE) ? clientRect.top : windowRect.top;
            var width = clientRect.right - left;
            var height = clientRect.bottom - top;

            this.Windows.Add(new(windowTitle, path, commandLine, hWnd, processId, left / eDpiScale, top / eDpiScale, width / eDpiScale, height / eDpiScale, --z));
        } while ((hWnd = GetNextWindow(hWnd, GetNextWindowCommands.GW_HWNDNEXT)) != 0);
    }

    partial void OnSelectedWindowChanged(WindowInfo? value)
    {
        if (value is null)
        {
            return;
        }
        this.Dialog!.DialogResult = true;
    }

    [RelayCommand]
    public void Cancel()
        => this.Dialog!.Close();

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    private interface IVirtualDesktopManager
    {
        bool IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow);
    }
}

public record WindowInfo(string Title, string Path, string CommandLine, nint Handle, int ProcessId, double Left, double Top, double Width, double Height, int ZOrder);

public sealed class WindowOffsetConverter : IMultiValueConverter
{
    public static WindowOffsetConverter Default { get; } = new WindowOffsetConverter();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [IObservableCollection<WindowInfo> windows, Window window])
        {
            return DependencyProperty.UnsetValue;
        }

        var rect = new Rect(window.Left, window.Top, window.Width, window.Height);

        var view = (window.Left != 0 || window.Top != 0)
            ? windows.CreateView(w => w with { Left = w.Left - rect.Left, Top = w.Top - rect.Top })
            : windows.CreateView(w => w);

        view.AttachFilter(w =>
        {
            var intersect = Rect.Intersect(rect, new(w.Left, w.Top, w.Width, w.Height));
            Debug.WriteLine(intersect);
            if (intersect != Rect.Empty)
            {
                return true;
            }
            else
            {
                return false;
            }
        });

        var list = view.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
        window.Closed += (s, e) => list.Dispose();

        return list;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
