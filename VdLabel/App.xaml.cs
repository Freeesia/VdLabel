using System.Windows;

namespace VdLabel;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly TaskCompletionSource tcs = new();
    public App()
    {
        InitializeComponent();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        this.tcs.SetResult();
    }

    public Task WaitForStartupAsync()
        => this.tcs.Task;
}
