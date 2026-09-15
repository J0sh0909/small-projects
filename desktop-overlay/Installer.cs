using System.Diagnostics;
using System.Reflection;
using System.Security;
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
/// The install is machine-wide rather than per-user. The logon trigger carries no
/// UserId, so it fires for whoever signs in, and the principal is the local
/// Administrators group rather than one account, so the task runs as that person
/// instead of as whoever happened to install it. Those two go together: an
/// any-user trigger with a fixed user principal silently does nothing when that
/// user isn't the one logging in, because it needs that account's own session.
///
/// Note there is no LogonType element on the principal: the task XML schema only
/// permits it alongside UserId, and schtasks rejects the whole document with
/// "unexpected node" if it appears next to a GroupId. A group principal runs in the
/// triggering member's interactive session anyway.
///
/// Task Scheduler is driven through schtasks.exe with a task XML document rather
/// than the COM API: it expresses every setting we need declaratively and needs
/// no interop.
/// </summary>
internal static class Installer
{
    private const string TaskName = "DesktopStats Overlay";
    private const string InstallDirName = "DesktopStats";

    /// <summary>
    /// Well-known SID of BUILTIN\Administrators, used in place of the name because
    /// the group is localised on non-English installs of Windows.
    ///
    /// Administrators rather than Users deliberately: app.manifest demands
    /// elevation, and HighestAvailable only raises a standard user to their own
    /// highest level, which still can't load the sensor driver.
    /// </summary>
    private const string AdministratorsSid = "S-1-5-32-544";

    /// <summary>
    /// DACL for the task: full control for SYSTEM and Administrators, read-only for
    /// authenticated users.
    ///
    /// Without this, a group-principal task inherits a default that only Administrators
    /// can read, and "Administrators" means the *unfiltered* token. Anyone browsing
    /// Task Scheduler normally, administrator or not, sees nothing at all and concludes
    /// the task was never created. Read is granted, but not execute: a standard user
    /// should be able to see the task without being able to trigger something that runs
    /// elevated.
    ///
    /// This has to be applied through the Task Scheduler COM interface after creation:
    /// schtasks /Create /XML stores the SecurityDescriptor element in the task document
    /// but never applies it, so the restrictive default survives. See ApplyTaskSecurity.
    /// </summary>
    private const string TaskSecurityDescriptor = "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FR;;;AU)";

    public static int Install(string[] args)
    {
        int delaySeconds = ArgValueInt(args, "--delay", 10);
        bool inPlace = args.Contains("--no-copy");

        string sourceExe = Environment.ProcessPath
            ?? throw new InvalidOperationException("Could not determine the running executable path.");
        string targetExe = inPlace ? sourceExe : CopyToInstallDir(sourceExe);

        if (inPlace && targetExe.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "Warning: --no-copy is registering an exe inside a user profile. Other\n" +
                "         administrators may not be able to read it at logon.");
        }

        Console.WriteLine("Registering logon task for any administrator");
        string xml = BuildTaskXml(targetExe, delaySeconds);

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

        ApplyTaskSecurity();

        Console.WriteLine($"Installed: {targetExe}");
        Console.WriteLine($"Task:      {TaskName}");
        Console.WriteLine($"Runs:      at logon of any administrator, {delaySeconds}s delay, elevated");

        Console.WriteLine("\nStarting the overlay...");
        var (runCode, runOutput) = RunSchtasks($"/Run /TN \"{TaskName}\"");
        if (runCode != 0)
        {
            Console.Error.WriteLine(runOutput.Trim());
            Console.WriteLine("The task is registered but did not start. It will still run at your next logon.");
            return 0;
        }

        Console.WriteLine("Done. DesktopStats will start automatically when any administrator logs on.");
        Console.WriteLine("Remove it later with:  DesktopStats.exe --uninstall");
        return 0;
    }

    /// <summary>
    /// Removes the machine-wide install. This affects every user on the machine, not
    /// just whoever runs it, because the task and the binaries are shared.
    /// </summary>
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

    /// <summary>
    /// %ProgramFiles%\DesktopStats. Machine-wide on purpose: a per-user location like
    /// %LOCALAPPDATA% lives inside one profile, which other administrators may not be
    /// able to read, so the task would fail for everyone but the installer.
    /// </summary>
    private static string InstallDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallDirName);

    /// <summary>
    /// Copies the binaries to %ProgramFiles%\DesktopStats so the scheduled task keeps
    /// working after the download folder is moved or deleted, and so every administrator
    /// can run it. Returns the new exe path.
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

    /// <summary>
    /// Stops any other running overlay so its files aren't locked. There can be more
    /// than one now that the task runs per session, so this sweeps them all.
    /// </summary>
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

    /// <summary>
    /// Relaxes the task's DACL so it is visible to a non-elevated user.
    ///
    /// Done through the Task Scheduler COM object because schtasks records the
    /// SecurityDescriptor element without applying it. Late-bound via reflection so the
    /// project needs no COM interop assembly and the single-file build stays portable.
    ///
    /// Failure here is not fatal: the task is already registered and working, it just
    /// wouldn't show up outside an elevated Task Scheduler.
    /// </summary>
    private static void ApplyTaskSecurity()
    {
        try
        {
            Type? serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType is null) return;

            object? service = Activator.CreateInstance(serviceType);
            if (service is null) return;

            InvokeCom(service, "Connect");
            object folder = InvokeCom(service, "GetFolder", "\\")!;
            object task = InvokeCom(folder, "GetTask", TaskName)!;
            InvokeCom(task, "SetSecurityDescriptor", TaskSecurityDescriptor, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: could not relax the task's permissions ({ex.Message}).");
            Console.WriteLine("      It works, but only appears in Task Scheduler when elevated.");
        }
    }

    private static object? InvokeCom(object target, string method, params object?[] args) =>
        target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);

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

    private static string BuildTaskXml(string exePath, int delaySeconds)
    {
        string workingDir = Path.GetDirectoryName(exePath)!;
        string delay = delaySeconds > 0
            ? $"\n      <Delay>PT{delaySeconds}S</Delay>"
            : "";

        return $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.3" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <URI>\{Esc(TaskName)}</URI>
            <SecurityDescriptor>{TaskSecurityDescriptor}</SecurityDescriptor>
            <Description>Real-time system stats rendered on the desktop wallpaper layer.</Description>
          </RegistrationInfo>
          <Triggers>
            <LogonTrigger>
              <Enabled>true</Enabled>{delay}
            </LogonTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <GroupId>{AdministratorsSid}</GroupId>
              <RunLevel>HighestAvailable</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
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
