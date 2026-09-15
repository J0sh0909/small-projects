using System.Runtime.InteropServices;

namespace DesktopStats;

static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new OverlayForm());
            return 0;
        }

        // This is a WinExe, so it has no console of its own. Borrow the caller's if
        // we were launched from one; otherwise open our own, which is what happens
        // when the exe is double-clicked, or when UAC relaunches it detached.
        bool ownConsole = !AttachConsole(-1) && AllocConsole();

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "--install" => Installer.Install(args),
                "--uninstall" => Installer.Uninstall(args),
                "--status" => Installer.Status(),
                "--dump-sensors" => DumpSensors(),
                "--help" or "-h" or "/?" => Usage(0),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            if (ownConsole)
            {
                Console.WriteLine("\nPress any key to close...");
                Console.ReadKey(intercept: true);
            }
        }
    }

    private static int DumpSensors()
    {
        using var reader = new SensorReader();
        reader.DumpCpuSensors();
        return 0;
    }

    private static int UnknownCommand(string arg)
    {
        Console.Error.WriteLine($"Unknown option: {arg}");
        return Usage(1);
    }

    private static int Usage(int exitCode)
    {
        Console.WriteLine("""
            DesktopStats - system stats on the desktop wallpaper layer.

              DesktopStats.exe                 Run the overlay.
              DesktopStats.exe --install       Install machine-wide, start at logon.
              DesktopStats.exe --uninstall     Remove the logon task and installed files.
              DesktopStats.exe --status        Show whether it is installed and running.
              DesktopStats.exe --dump-sensors  Print every CPU sensor, for diagnostics.
              DesktopStats.exe --help          Show this message.

            --install copies the exe to %ProgramFiles%\DesktopStats and registers a task
            that starts it whenever any administrator logs on, elevated, in their own
            session. Both --install and --uninstall affect every user on the machine.

            Options for --install:
              --delay <seconds>   Wait this long after logon before starting. Default 10.
              --no-copy           Register where the exe already is; don't copy it to
                                  %ProgramFiles%\DesktopStats.

            Options for --uninstall:
              --keep-files        Remove the logon task but leave the binaries on disk.
            """);
        return exitCode;
    }
}
