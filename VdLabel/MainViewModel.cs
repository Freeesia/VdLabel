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
    private readonly IVirualDesktopService virualDesktopService;
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

    public MainViewModel(IConfigStore configStore, IContentDialogService dialogService, IVirualDesktopService virualDesktopService)
    {
        this.configStore = configStore;
        this.dialogService = dialogService;
        this.virualDesktopService = virualDesktopService;
        this.virualDesktopService.DesktopChanged += VirualDesktopService_DesktopChanged;
        this.isStartup = GetIsStartup();
        Load();
    }

    private void VirualDesktopService_DesktopChanged(object? sender, DesktopChangedEventArgs e)
         => this.SelectedDesktopConfig = this.DesktopConfigs.FirstOrDefault(c => c.Id == e.DesktopId) ?? this.DesktopConfigs.FirstOrDefault();

    private async void Load()
    {
        this.IsBusy = true;
        this.configStore.Saved -= ConfigStore_Saved;
        try
        {
            this.Config = await this.configStore.Load();
            this.DesktopConfigs.Clear();
            foreach (var desktopConfig in this.Config.DesktopConfigs)
            {
                this.DesktopConfigs.Add(new DesktopConfigViewModel(desktopConfig, this.virualDesktopService));
            }
            this.SelectedDesktopConfig = this.DesktopConfigs.FirstOrDefault();
        }
        finally
        {
            this.IsBusy = false;
        }
        this.configStore.Saved += ConfigStore_Saved;
    }

    private void ConfigStore_Saved(object? sender, EventArgs e)
        => Load();

    [RelayCommand]
    public async Task Save()
    {
        this.IsBusy = true;
        this.configStore.Saved -= ConfigStore_Saved;
        try
        {
            this.Config!.DesktopConfigs.Clear();
            foreach (var desktopConfig in this.DesktopConfigs)
            {
                this.Config.DesktopConfigs.Add(desktopConfig.GetSaveConfig());
            }
            await this.configStore.Save(this.Config);
        }
        finally
        {
            this.IsBusy = false;
        }
        this.configStore.Saved += ConfigStore_Saved;
    }

    [RelayCommand]
    public async Task ReloadDesktops()
    {
        this.IsBusy = true;
        try
        {
            await this.virualDesktopService.ReloadDesktops();
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
            key.SetValue(name, path);
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

partial class DesktopConfigViewModel(DesktopConfig desktopConfig, IVirualDesktopService virualDesktopService) : ObservableObject
{
    private readonly IVirualDesktopService virualDesktopService = virualDesktopService;

    public Guid Id { get; } = desktopConfig.Id;

    public bool IsPin => this.Id == Guid.Empty;
    public bool IsNotPin => !this.IsPin;

    public string Title => this.IsPin ? "全デスクトップ" : string.IsNullOrEmpty(this.Name) ? this.Id.ToString() : this.Name;

    [ObservableProperty]
    private bool isVisibleName = desktopConfig.IsVisibleName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    private string? name = desktopConfig.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisibleImage))]
    private string? imagePath = desktopConfig.ImagePath;

    public bool ShowNameWarning => !this.virualDesktopService.IsSupportedName;

    public bool IsVisibleImage => this.ImagePath is not null;

    public ObservableCollection<WindowConfig> TargetWindows { get; } = new(desktopConfig.TargetWindows);

    public IReadOnlyList<WindowMatchType> MatchTypes { get; } = Enum.GetValues<WindowMatchType>();

    public IReadOnlyList<WindowPatternType> PatternTypes { get; } = Enum.GetValues<WindowPatternType>();

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

    [RelayCommand]
    public void AddTargetWindow()
        => this.TargetWindows.Add(new());

    [RelayCommand]
    public void RemoveTargetWindow(WindowConfig target)
        => this.TargetWindows.Remove(target);

    public DesktopConfig GetSaveConfig()
        => new()
        {
            Id = this.Id,
            Name = this.Name,
            IsVisibleName = this.IsVisibleName,
            ImagePath = this.ImagePath,
            TargetWindows = this.TargetWindows.ToArray(),
        };
}
