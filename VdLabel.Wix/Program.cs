using System;
using System.Diagnostics;
using WixSharp;
using Path = System.IO.Path;

const string App = "VdLabel";
const string ArtifactsDir = @"..\artifacts";
const string PublishDir = @"..\publish";
const string Executable = $"{App}.exe";

var exePath = Path.Combine(Environment.CurrentDirectory, ArtifactsDir, Executable);
var info = FileVersionInfo.GetVersionInfo(exePath);
var version = info.FileVersion;

var project = new ManagedProject(App,
    new Dir(@$"%ProgramFiles%\StudioFreesia\{App}",
        new File(exePath) { AddCloseAction = true },
        new Files(Path.Combine(ArtifactsDir, "*.*"), p => !p.EndsWith(Executable))));

project.RebootSupressing = RebootSupressing.Suppress;
project.GUID = new("FE947636-81DB-4819-A5D9-939125903F4C");
project.Platform = Platform.x64;
project.Language = "ja-JP";
project.Version = new(version);

// どっちか片方しか設定できない
//project.MajorUpgrade = MajorUpgrade.Default;
project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;

project.BackgroundImage = @"..\assets\installer_back.png";
project.ValidateBackgroundImage = false;
project.BannerImage = @"..\assets\installer_bunner.png";

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

project.BuildMsi(Path.Combine(PublishDir, $"{App}_{version}.msi"));
