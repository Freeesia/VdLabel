using System.Windows;
using Kamishibai;

namespace VdLabel;

public partial interface IPresentationService : IPresentationServiceBase
{
    Task<WindowInfo?> OpenTargetWindowDialogAsync();
}

public class PresentationService(IServiceProvider serviceProvider, INavigationFrameProvider navigationFrameProvider, IWindowService windowService)
    : PresentationServiceBase(serviceProvider, navigationFrameProvider, windowService), IPresentationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task<WindowInfo?> OpenTargetWindowDialogAsync()
    {
        var current = Application.Current.Windows.OfType<Window>().SingleOrDefault(x => x.IsActive);
        current?.Hide();
        try
        {
            var vm = new TargetWindowViewModel();
            if (await OpenDialogAsync(vm) == true)
            {
                return vm.SelectedWindow;
            }
            else
            {
                return null;
            }
        }
        finally
        {
            current?.Show();
        }
    }
}