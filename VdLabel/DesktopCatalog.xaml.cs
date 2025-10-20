using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Ui.Controls;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using static Windows.Win32.PInvoke;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace VdLabel;

/// <summary>
/// DesktopCatalog.xaml の相互作用ロジック
/// </summary>
public partial class DesktopCatalog : FluentWindow
{
    private readonly IVirualDesktopService virualDesktopService;
    private Point startPoint;
    private bool isDragging = false;

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

    private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        startPoint = e.GetPosition(null);
        isDragging = false;

        if (sender is ListBoxItem item)
        {
            item.PreviewMouseMove += ListBoxItem_PreviewMouseMove;
        }
    }

    private void ListBoxItem_PreviewMouseMove(object? sender, MouseEventArgs e)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed && !isDragging)
        {
            Point currentPosition = e.GetPosition(null);
            Vector diff = startPoint - currentPosition;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is ListBoxItem item && item.Content is DesktopViewModel desktop)
                {
                    isDragging = true;
                    item.PreviewMouseMove -= ListBoxItem_PreviewMouseMove;
                    
                    DragDrop.DoDragDrop(item, desktop, DragDropEffects.Move);
                    
                    isDragging = false;
                }
            }
        }
    }

    private void ListBoxItem_Drop(object sender, DragEventArgs e)
    {
        if (!this.virualDesktopService.IsSupportedMoveDesktop)
        {
            return;
        }

        if (e.Data.GetData(typeof(DesktopViewModel)) is DesktopViewModel sourceDesktop &&
            sender is ListBoxItem targetItem &&
            targetItem.Content is DesktopViewModel targetDesktop &&
            sourceDesktop.Id != targetDesktop.Id &&
            this.DataContext is DesktopCatalogViewModel viewModel)
        {
            var sourceIndex = viewModel.Desktops.ToList().FindIndex(d => d.Id == sourceDesktop.Id);
            var targetIndex = viewModel.Desktops.ToList().FindIndex(d => d.Id == targetDesktop.Id);

            if (sourceIndex >= 0 && targetIndex >= 0)
            {
                // VirtualDesktop API uses 0-based index
                this.virualDesktopService.MoveDesktop(sourceDesktop.Id, targetIndex);
            }
        }
    }
}
