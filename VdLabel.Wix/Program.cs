using System.Diagnostics;
using WixSharp;

const string App = "VdLabel";
const string Executable = $"{App}.exe";
const string Version = "0.0.2.0";

var project = new ManagedProject(App,
    new Dir(@$"%ProgramFiles%\StudioFreesia\{App}", new File(@$"..\{Executable}") { AddCloseAction = true }));

project.RebootSupressing = RebootSupressing.Suppress;
project.GUID = new("FE947636-81DB-4819-A5D9-939125903F4C");
project.Platform = Platform.x64;
project.Language = "ja-JP";
project.Version = new(Version);

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

project.BuildMsi($"{App}_{Version}.msi");
