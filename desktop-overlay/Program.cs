using System.Runtime.InteropServices;

namespace DesktopStats;

static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--dump-sensors"))
        {
            if (!AttachConsole(-1)) AllocConsole();
            using var reader = new SensorReader();
            reader.DumpCpuSensors();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayForm());
    }
}
