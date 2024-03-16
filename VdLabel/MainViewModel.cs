using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Reflection;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace VdLabel;

partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore configStore;
    private readonly IContentDialogService dialogService;
    private readonly IVirtualDesktopCompat virtualDesktopCompat;
    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private Config? config;

    [ObservableProperty]
    private DesktopConfigViewModel? selectedDesktopConfig;

    [ObservableProperty]
    private bool isStartup;

    public ObservableCollection<DesktopConfigViewModel> DesktopConfigs { get; } = [];

    public IReadOnlyList<OverlayPosition> Positions { get; } = Enum.GetValues<OverlayPosition>();

    public MainViewModel(IConfigStore configStore, IContentDialogService dialogService, IVirtualDesktopCompat virtualDesktopCompat)
    {
        this.configStore = configStore;
        this.dialogService = dialogService;
        this.virtualDesktopCompat = virtualDesktopCompat;
        this.isStartup = GetIsStartup();
        Load();
    }

    private async void Load()
    {
        this.IsBusy = true;
        try
        {
            this.Config = await this.configStore.Load();
            this.DesktopConfigs.Clear();
            foreach (var desktopConfig in this.Config.DesktopConfigs)
            {
                this.DesktopConfigs.Add(new DesktopConfigViewModel(desktopConfig, this.virtualDesktopCompat));
            }
            this.SelectedDesktopConfig = this.DesktopConfigs.FirstOrDefault();
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task Save()
    {
        this.IsBusy = true;
        try
        {
            this.Config!.DesktopConfigs.Clear();
            foreach (var desktopConfig in this.DesktopConfigs)
            {
                this.Config.DesktopConfigs.Add(desktopConfig.DesktopConfig);
            }
            await this.configStore.Save(this.Config);
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    partial void OnIsStartupChanged(bool value)
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true) ?? throw new InvalidOperationException();
        var name = exe.GetName().Name ?? throw new InvalidOperationException();
        var path = Environment.ProcessPath ?? throw new InvalidOperationException();
        if (value)
        {
            key.SetValue(name, exe.Location);
            this.dialogService.ShowAlertAsync("自動起動", $"{name}を自動起動に登録しました。", "OK");
        }
        else
        {
            key.DeleteValue(name, false);
            this.dialogService.ShowAlertAsync("自動起動", $"{name}の自動起動を解除しました。", "OK");
        }
    }

    private static bool GetIsStartup()
    {
        var exe = Assembly.GetExecutingAssembly();
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        string? name = exe.GetName().Name;
        return key?.GetValue(name) is { };
    }
}

partial class DesktopConfigViewModel(DesktopConfig desktopConfig, IVirtualDesktopCompat virtualDesktopCompat) : ObservableObject
{
    private readonly IVirtualDesktopCompat virtualDesktopCompat = virtualDesktopCompat;

    public DesktopConfig DesktopConfig { get; } = desktopConfig;

    public Guid Id => this.DesktopConfig.Id;

    public bool IsDefault => this.DesktopConfig.Id == Guid.Empty;

    public string Title => this.IsDefault ? "デフォルト設定" : this.DesktopConfig.Name ?? this.DesktopConfig.Id.ToString();

    public bool IsVisibleName
    {
        get => this.DesktopConfig.IsVisibleName;
        set => SetProperty(this.DesktopConfig.IsVisibleName, value, this.DesktopConfig, (c, v) => c.IsVisibleName = v);
    }

    public string? Name
    {
        get => this.DesktopConfig.Name;
        set
        {
            if (SetProperty(this.DesktopConfig.Name, value, this.DesktopConfig, (c, v) => c.Name = v))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public bool ShowNameWarning => !this.virtualDesktopCompat.IsSupportedName;

    public string? ImagePath
    {
        get => this.DesktopConfig.ImagePath;
        set
        {
            if (SetProperty(this.DesktopConfig.ImagePath, value, this.DesktopConfig, (c, v) => c.ImagePath = v))
            {
                OnPropertyChanged(nameof(IsVisibleImage));
            }
        }
    }

    public bool IsVisibleImage => this.ImagePath is not null;

    [RelayCommand]
    public void PickImage()
    {
        var openFileDialog = new OpenFileDialog()
        {
            DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            AddToRecent = true,
            CheckFileExists = true,
            ForcePreviewPane = true,
            Filter = "Image files (*.bmp;*.jpg;*.jpeg;*.png)|*.bmp;*.jpg;*.jpeg;*.png|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        this.ImagePath = openFileDialog.FileName;
    }
}