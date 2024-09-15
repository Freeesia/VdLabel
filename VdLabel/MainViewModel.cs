using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Data;
using Wpf.Ui;
using Wpf.Ui.Extensions;

namespace VdLabel;

partial class MainViewModel : ObservableObject
{
    private readonly IConfigStore configStore;
    private readonly IContentDialogService dialogService;
    private readonly IVirualDesktopService virualDesktopService;
    private readonly ICommandLabelService commandLabelService;
    private readonly IUpdateChecker updateChecker;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private Config? config;

    [ObservableProperty]
    private DesktopConfigViewModel? selectedDesktopConfig;

    [ObservableProperty]
    private bool isStartup;

    [ObservableProperty]
    private bool hasUpdate;

    [ObservableProperty]
    private string? newVersion;

    private string? newVersionUrl;
    private string? installPath;

    public string Title { get; } = $"VdLabel {Assembly.GetExecutingAssembly().GetName().Version}";

    public ObservableCollection<DesktopConfigViewModel> DesktopConfigs { get; } = [];

    public IReadOnlyList<OverlayPosition> OverlayPositions { get; } = Enum.GetValues<OverlayPosition>();
    public IReadOnlyList<NamePosition> NamePositions { get; } = Enum.GetValues<NamePosition>();

    public MainViewModel(
        IConfigStore configStore,
        IContentDialogService dialogService,
        IVirualDesktopService virualDesktopService,
        ICommandLabelService commandLabelService,
        IUpdateChecker updateChecker)
    {
        BindingOperations.EnableCollectionSynchronization(this.DesktopConfigs, new());
        this.configStore = configStore;
        this.dialogService = dialogService;
        this.virualDesktopService = virualDesktopService;
        this.commandLabelService = commandLabelService;
        this.updateChecker = updateChecker;
        this.virualDesktopService.DesktopChanged += VirualDesktopService_DesktopChanged;
        this.updateChecker.UpdateAvailable += UpdateChecker_UpdateAvailable;
        this.isStartup = GetIsStartup();
        SetUpUpdateInfo();
        Load();
    }

    private void UpdateChecker_UpdateAvailable(object? sender, EventArgs e)
        => SetUpUpdateInfo();

    private async void SetUpUpdateInfo()
    {
        if (this.updateChecker.HasUpdate && await this.configStore.LoadUpdateInfo() is { } info && !info.Skip)
        {
            this.NewVersion = info.Version;
            this.newVersionUrl = info.Url;
            this.installPath = info.Path;
            this.HasUpdate = true;
        }
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
                this.DesktopConfigs.Add(new(desktopConfig, this.dialogService, this.virualDesktopService, this.commandLabelService));
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

    [RelayCommand]
    public void OpenReleaseNotes()
        => Process.Start(new ProcessStartInfo(this.newVersionUrl!) { UseShellExecute = true });

    [RelayCommand]
    public void InstallUpdate()
        => Process.Start("msiexec", $"/i {this.installPath}");

    [RelayCommand]
    public Task CheckUpdate(CancellationToken token)
        => this.updateChecker.Check(token);

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

partial class DesktopConfigViewModel(DesktopConfig desktopConfig, IContentDialogService dialogService, IVirualDesktopService virualDesktopService, ICommandLabelService commandLabelService) : ObservableObject
{
    private readonly IContentDialogService dialogService = dialogService;
    private readonly IVirualDesktopService virualDesktopService = virualDesktopService;
    private readonly ICommandLabelService commandLabelService = commandLabelService;

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
    [NotifyCanExecuteChangedFor(nameof(TestCommandCommand))]
    private string? command = desktopConfig.Command;

    [ObservableProperty]
    private bool utf8Command = desktopConfig.Utf8Command;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisibleImage))]
    [NotifyCanExecuteChangedFor(nameof(RemoveImageCommand))]
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

    [RelayCommand(CanExecute = nameof(CanRemoveImage))]
    public void RemoveImage()
        => this.ImagePath = null;

    private bool CanRemoveImage
        => this.ImagePath is not null;

    [RelayCommand(CanExecute = nameof(CanTestCommand))]
    public async Task TestCommand()
    {
        var command = this.Command ?? throw new InvalidOperationException();
        try
        {
            var result = await this.commandLabelService.ExecuteCommand(command, this.Utf8Command);
            await this.dialogService.ShowAlertAsync("コマンド成功", result, "OK");
        }
        catch (Exception e)
        {
            await this.dialogService.ShowAlertAsync("コマンド失敗", e.Message, "OK");
        }
    }

    private bool CanTestCommand
        => !string.IsNullOrEmpty(this.Command);

    [RelayCommand]
    public void AddTargetWindow()
        => this.TargetWindows.Add(new());

    [RelayCommand]
    public void RemoveTargetWindow(WindowConfig target)
        => this.TargetWindows.Remove(target);

    [RelayCommand]
    public void FindWindow()
    {
        Debug.WriteLine("FindWindow");
    }

    public DesktopConfig GetSaveConfig()
        => new()
        {
            Id = this.Id,
            IsVisibleName = this.IsVisibleName,
            Name = this.Name,
            Utf8Command = this.Utf8Command,
            Command = this.Command,
            ImagePath = this.ImagePath,
            TargetWindows = this.TargetWindows.ToArray(),
        };
}
