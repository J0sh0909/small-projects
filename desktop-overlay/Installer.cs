using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace DesktopStats;

/// <summary>
/// Self-install: registers DesktopStats as an elevated logon task so it starts
/// with Windows.
///
/// The overlay needs administrator rights (LibreHardwareMonitor loads a kernel
/// driver to read sensors), which rules out the Startup folder and the registry
/// Run key: Windows won't elevate either silently. A scheduled task with
/// RunLevel=HighestAvailable will, so that's what we register.
///
/// Task Scheduler is driven through schtasks.exe with a task XML document rather
/// than the COM API: it expresses every setting we need declaratively and needs
/// no interop.
/// </summary>
internal static class Installer
{
    private const string TaskName = "DesktopStats Overlay";
    private const string InstallDirName = "DesktopStats";

    public static int Install(string[] args)
    {
        int delaySeconds = ArgValueInt(args, "--delay", 10);
        bool inPlace = args.Contains("--no-copy");
        string account = ArgValue(args, "--user") ?? WindowsIdentity.GetCurrent().Name;

        string sourceExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable path.");
        string targetExe = inPlace ? sourceExe : CopyToInstallDir(sourceExe);

        Console.WriteLine($"Registering logon task for {account}");
        string xml = BuildTaskXml(targetExe, account, delaySeconds);

        // schtasks reads the definition from a file; UTF-16 is the encoding it
        // documents, and a BOM-less UTF-8 file is rejected on some builds.
        string xmlPath = Path.Combine(Path.GetTempPath(), $"DesktopStats-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(xmlPath, xml, new UnicodeEncoding(false, true));

            var (code, output) = RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{xmlPath}\" /F");
            if (code != 0)
            {
                Console.Error.WriteLine(output.Trim());
                Console.Error.WriteLine($"\nFailed to register the scheduled task (schtasks exit {code}).");
                return code;
            }
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { /* temp file; nothing to do */ }
        }

        Console.WriteLine($"Installed: {targetExe}");
        Console.WriteLine($"Task:      {TaskName}  (at logon, {delaySeconds}s delay, elevated)");

        Console.WriteLine("\nStarting the overlay...");
        var (runCode, runOutput) = RunSchtasks($"/Run /TN \"{TaskName}\"");
        if (runCode != 0)
        {
            Console.Error.WriteLine(runOutput.Trim());
            Console.WriteLine("The task is registered but did not start. It will still run at your next logon.");
            return 0;
        }

        Console.WriteLine("Done. DesktopStats will start automatically at every logon.");
        Console.WriteLine("Remove it later with:  DesktopStats.exe --uninstall");
        return 0;
    }

    public static int Uninstall(string[] args)
    {
        bool keepFiles = args.Contains("--keep-files");

        var (code, output) = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
        if (code == 0)
            Console.WriteLine($"Removed scheduled task '{TaskName}'.");
        else
            Console.WriteLine($"No scheduled task '{TaskName}' to remove. ({output.Trim()})");

        StopRunningOverlay();

        if (keepFiles)
        {
            Console.WriteLine("Leaving installed files in place (--keep-files).");
            return 0;
        }

        string installDir = InstallDir;
        if (!Directory.Exists(installDir))
        {
            Console.WriteLine("Nothing installed to remove.");
            return 0;
        }

        // If we're running from inside the directory we're deleting, our own exe is
        // locked. Hand the final rmdir to a detached shell that starts after we exit.
        string self = Environment.ProcessPath ?? "";
        if (self.StartsWith(installDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            ScheduleSelfDelete(installDir);
            Console.WriteLine($"Removing {installDir} on exit.");
            return 0;
        }

        try
        {
            Directory.Delete(installDir, recursive: true);
            Console.WriteLine($"Removed {installDir}.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not remove {installDir}: {ex.Message}");
            return 1;
        }

        return 0;
    }

    public static int Status()
    {
        var (code, output) = RunSchtasks($"/Query /TN \"{TaskName}\" /FO LIST");
        if (code != 0)
        {
            Console.WriteLine($"Not installed (no scheduled task '{TaskName}').");
            return 1;
        }

        Console.WriteLine(output.Trim());

        string installDir = InstallDir;
        Console.WriteLine(Directory.Exists(installDir)
            ? $"\nInstall directory: {installDir}"
            : $"\nInstall directory: {installDir} (missing; task may point elsewhere)");

        var running = Process.GetProcessesByName("DesktopStats")
                             .Where(p => p.Id != Environment.ProcessId)
                             .ToArray();
        Console.WriteLine(running.Length > 0
            ? $"Running: yes (PID {string.Join(", ", running.Select(p => p.Id))})"
            : "Running: no");

        return 0;
    }

    private static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), InstallDirName);

    /// <summary>
    /// Copies the binaries to %LOCALAPPDATA%\DesktopStats so the scheduled task keeps
    /// working after the download folder is moved or deleted. Returns the new exe path.
    /// </summary>
    private static string CopyToInstallDir(string sourceExe)
    {
        string sourceDir = Path.GetDirectoryName(sourceExe)!;
        string installDir = InstallDir;

        if (string.Equals(sourceDir.TrimEnd('\\'), installDir.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Already running from the install directory; skipping copy.");
            return sourceExe;
        }

        StopRunningOverlay();
        Directory.CreateDirectory(installDir);

        // The self-contained build is a lone exe; the framework-dependent build needs
        // its sibling assemblies, so bring the whole folder in that case.
        bool hasSiblings = Directory
            .EnumerateFiles(sourceDir, "*.dll")
            .Any();

        if (hasSiblings)
        {
            Console.WriteLine($"Copying binaries to {installDir}");
            CopyDirectory(sourceDir, installDir);
        }
        else
        {
            Console.WriteLine($"Copying DesktopStats.exe to {installDir}");
            File.Copy(sourceExe, Path.Combine(installDir, Path.GetFileName(sourceExe)), overwrite: true);
        }

        return Path.Combine(installDir, Path.GetFileName(sourceExe));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);

        foreach (string dir in Directory.EnumerateDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    /// <summary>Stops any other running overlay so its files aren't locked.</summary>
    private static void StopRunningOverlay()
    {
        foreach (var process in Process.GetProcessesByName("DesktopStats"))
        {
            if (process.Id == Environment.ProcessId) continue;

            try
            {
                Console.WriteLine($"Stopping running overlay (PID {process.Id})...");
                process.Kill();
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not stop PID {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void ScheduleSelfDelete(string directory)
    {
        // timeout gives this process time to exit and release its own image.
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c timeout /t 3 /nobreak >nul & rd /s /q \"{directory}\"",
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        Process.Start(psi);
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not start schtasks.exe.");

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout + stderr);
    }

    private static string BuildTaskXml(string exePath, string account, int delaySeconds)
    {
        string workingDir = Path.GetDirectoryName(exePath)!;
        string delay = delaySeconds > 0
            ? $"\n      <Delay>PT{delaySeconds}S</Delay>"
            : "";

        return $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.3" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Description>Real-time system stats rendered on the desktop wallpaper layer.</Description>
            <URI>\{Esc(TaskName)}</URI>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>
              <UserId>{Esc(account)}</UserId>{delay}
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{Esc(account)}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <AllowHardTerminate>true</AllowHardTerminate>
            <StartWhenAvailable>false</StartWhenAvailable>
            <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
            <IdleSettings>
              <StopOnIdleEnd>false</StopOnIdleEnd>
              <RestartOnIdle>false</RestartOnIdle>
            </IdleSettings>
            <AllowStartOnDemand>true</AllowStartOnDemand>
            <Enabled>true</Enabled>
            <Hidden>false</Hidden>
            <RunOnlyIfIdle>false</RunOnlyIfIdle>
            <WakeToRun>false</WakeToRun>
            <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
            <Priority>7</Priority>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{Esc(exePath)}</Command>
              <WorkingDirectory>{Esc(workingDir)}</WorkingDirectory>
            </Exec>
          </Actions>
        </Task>
        """;
    }

    private static string Esc(string value) => SecurityElement.Escape(value) ?? value;

    private static string? ArgValue(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int ArgValueInt(string[] args, string name, int fallback) =>
        int.TryParse(ArgValue(args, name), out int value) ? value : fallback;
}
