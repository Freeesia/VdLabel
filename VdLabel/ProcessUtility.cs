using System.Diagnostics;
using System.Management;
using PInvoke;

namespace VdLabel;
public static class ProcessUtility
{
    public static string? GetWindowTitle(nint hWnd)
    {
        try
        {
            return User32.GetWindowText(hWnd);
        }
        catch (Win32Exception)
        {
            // 仮想デスクトップを切り替えるタイミングで例外が発生することがある
            return null;
        }
    }

    public static string? GetProcessPath(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            using var module = process.MainModule;
            return module?.FileName ?? string.Empty;
        }
        catch (Exception)
        {
            // プロセスが終了している場合がある
            return null;
        }
    }

    public static string? GetCommandLine(int processId)
    {
        using var searcher = new ManagementObjectSearcher($"SELECT CommandLine FROM Win32_Process WHERE ProcessId = '{processId}'");
        using var mo = searcher.Get().Cast<ManagementBaseObject>().SingleOrDefault();
        return mo?["CommandLine"] as string;
    }
}
