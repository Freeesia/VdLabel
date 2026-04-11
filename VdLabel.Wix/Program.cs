using System;
using System.Diagnostics;
using WixSharp;
using Path = System.IO.Path;

const string Manufacturer = "StudioFreesia";
const string App = "VdLabel";
const string ArtifactsDir = @"..\artifacts";
const string Executable = $"{App}.exe";

var exePath = Path.Combine(Environment.CurrentDirectory, ArtifactsDir, Executable);
var info = FileVersionInfo.GetVersionInfo(exePath);
var version = info.FileVersion;

var project = new ManagedProject(App,
    new Dir(@$"%LocalAppData%\{Manufacturer}\{App}",
        new File(exePath) { AddCloseAction = true },
        new Files(Path.Combine(ArtifactsDir, "*.*"), p => !p.EndsWith(Executable))),
    // スタートメニューにショートカットを追加
    new Dir(@$"%ProgramMenu%\{Manufacturer}\{App}",
        new ExeFileShortcut(App, $"[INSTALLDIR]{App}", "")));

project.RebootSupressing = RebootSupressing.Suppress;
project.GUID = new("FE947636-81DB-4819-A5D9-939125903F4C");
project.Platform = Platform.x64;
project.Version = new(version);

// コントロールパネルの情報を設定
project.ControlPanelInfo = new()
{
    Manufacturer = Manufacturer,
    ProductIcon = @"..\VdLabel\app.ico",
    UrlInfoAbout = "https://github.com/Freeesia/VdLabel",
    UrlUpdateInfo = "https://github.com/Freeesia/VdLabel/releases",
};

project.MajorUpgrade = MajorUpgrade.Default;

project.BackgroundImage = @"..\assets\installer_back.png";
project.ValidateBackgroundImage = false;
project.BannerImage = @"..\assets\installer_bunner.png";

// ユーザーレベルのインストールを強制する
project.Scope = InstallScope.perUser;

// ライセンスファイルの設定
project.LicenceFile = @"..\LICENSE.rtf";

// インストール後にアプリを起動するオプション
project.AfterInstall += static e =>
{
    // アンインストール時には起動しない
    if (!e.IsUninstalling)
    {
        Process.Start(e.InstallDir.PathCombine(Executable));
    }
};

// WXS ファイルを generated/ フォルダに生成する（MSI のビルドは VdLabel.Installer.wixproj で行う）
// generated/ は .gitignore で除外されているため、生成物がリポジトリに混入しない
project.OutDir = "generated";
Compiler.BuildWxs(project);
