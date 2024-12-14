using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Controls;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static Windows.Win32.PInvoke;
using System.Runtime.InteropServices;
using System.Windows;

namespace VdLabel;

/// <summary>
/// DesktopCatalog.xaml の相互作用ロジック
/// </summary>
public partial class DesktopCatalog : FluentWindow
{
    private readonly IVirualDesktopService virualDesktopService;

    public DesktopCatalog(IVirualDesktopService virualDesktopService)
    {
        InitializeComponent();
        this.virualDesktopService = virualDesktopService;
    }

    private void FluentWindow_Closed(object sender, EventArgs e)
        => this.virualDesktopService.IsEnableOverlay = true;

    private void CloseCommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        => Close();

    protected override void OnActivated(EventArgs e)
    {
        var windowHandle = new WindowInteropHelper(this).Handle;

        this.virualDesktopService.IsEnableOverlay = false;
        this.Dispatcher.InvokeAsync(() =>
        {
            this.virualDesktopService.Pin(this);
            ToForeground();
            this.desktops.Focus();
            if (this.desktops.ItemContainerGenerator.ContainerFromIndex(this.desktops.SelectedIndex) is UIElement item)
            {
                item.Focus();
            }
        });
    }

    private void ToForeground()
    {
        // 空のマウスイベントを送信して強制的にアクティブ化
        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        SendInput([input], Marshal.SizeOf(input));
        Activate();
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        // ESC キーで閉じる場合は、閉じてから非アクティブになるので、表示されているときだけ閉じる
        if (this.IsVisible)
        {
            Close();
        }
    }
}
