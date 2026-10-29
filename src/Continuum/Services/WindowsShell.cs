using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Continuum.Services;

/// <summary>Windows-only touches: taskbar identity and Start menu shortcuts.</summary>
public static class WindowsShell
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void SetAppId(string appId)
    {
        if (OperatingSystem.IsWindows()) SetCurrentProcessExplicitAppUserModelID(appId);
    }

    /// <summary>Starts another app as its own process, so it gets its own taskbar button.</summary>
    public static void Launch(AppInfo app)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Continuum.exe");
        Process.Start(new ProcessStartInfo(exe, $"--app {app.Argument}") { UseShellExecute = false });
    }

    /// <summary>One Start menu entry per app, each with its own icon, like separate programs.</summary>
    public static void CreateStartMenuShortcuts(Func<AppInfo, byte[]> renderIcon)
    {
        if (!OperatingSystem.IsWindows()) return;
        var exe = Environment.ProcessPath!;
        var iconDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Continuum", "icons");
        var menuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Continuum");
        Directory.CreateDirectory(iconDir);
        Directory.CreateDirectory(menuDir);

        var script = new System.Text.StringBuilder("$sh = New-Object -ComObject WScript.Shell\n");
        foreach (var app in AppInfo.All)
        {
            var ico = Path.Combine(iconDir, app.Argument + ".ico");
            File.WriteAllBytes(ico, Ico.FromPng(renderIcon(app)));
            var lnk = Path.Combine(menuDir, app.Name + ".lnk");
            script.Append($"$s = $sh.CreateShortcut('{Escape(lnk)}'); $s.TargetPath = '{Escape(exe)}'; ")
                  .Append($"$s.Arguments = '--app {app.Argument}'; $s.IconLocation = '{Escape(ico)}'; ")
                  .Append($"$s.WorkingDirectory = '{Escape(AppContext.BaseDirectory)}'; $s.Save()\n");
        }

        var file = Path.Combine(Path.GetTempPath(), "continuum-shortcuts.ps1");
        File.WriteAllText(file, script.ToString());
        using var p = Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        p?.WaitForExit(15000);
    }

    static string Escape(string s) => s.Replace("'", "''");
}

/// <summary>Wraps a PNG in a single-image .ico file (supported since Windows Vista).</summary>
public static class Ico
{
    public static byte[] FromPng(byte[] png)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)1); w.Write((short)1);          // header: icon, 1 image
        w.Write((byte)0); w.Write((byte)0);                              // 256 x 256
        w.Write((byte)0); w.Write((byte)0);                              // no palette
        w.Write((short)1); w.Write((short)32);                           // planes, bpp
        w.Write(png.Length); w.Write(6 + 16);                            // size, offset
        w.Write(png);
        return ms.ToArray();
    }
}
