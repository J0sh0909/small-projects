using System.Diagnostics;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace DesktopStats;

public class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private readonly float _installedRamGb;
    private readonly PerformanceCounter? _cpuLoadCounter;

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out long totalMemoryInKilobytes);

    public SensorReader()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
        };
        _computer.Open();

        // Get actual installed RAM (e.g. 32 GB, not what the OS reports)
        if (GetPhysicallyInstalledSystemMemory(out long totalKb))
            _installedRamGb = totalKb / (1024f * 1024f);
        else
            _installedRamGb = 0;

        // LibreHardwareMonitor's CPU "Load" sensors derive idle time from
        // NtQuerySystemInformation(SystemProcessorIdleInformation), which tracks C-state
        // (idle-state) residency rather than actual work done. When the active power plan's
        // "Maximum processor state" is pinned at 100%, Windows/CPPC keeps cores parked in an
        // active P-state instead of demoting to a real idle C-state, so that idle-time delta
        // collapses to ~0 and every core reports ~100% load even when the CPU is doing nothing.
        //
        // The "fixed" alternative, "% Processor Utility" (Processor Information category), is
        // *also* driven by the same hardware idle/frequency APIs and was empirically observed on
        // this machine reading >170% while genuinely idle - it's skewed by turbo/P-state too.
        // "% Processor Time" (Processor category) instead comes from software idle-thread
        // scheduling accounting (which thread the scheduler actually dispatched), which is
        // unaffected by C-state/P-state pinning, and it was verified to track real load
        // correctly (near 0% idle, ~90%+ saturated with a busy loop). Use that for CpuLoadPercent.
        try
        {
            _cpuLoadCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuLoadCounter.NextValue(); // first sample is always 0; prime it
        }
        catch
        {
            _cpuLoadCounter = null;
        }
    }

    public SystemSnapshot Read()
    {
        var snap = new SystemSnapshot();
        snap.RamTotalGb = _installedRamGb;

        if (_cpuLoadCounter != null)
        {
            try
            {
                snap.CpuLoadPercent = Math.Clamp(_cpuLoadCounter.NextValue(), 0f, 100f);
            }
            catch
            {
                // Fall through and let ReadCpu() below fill in the LHM sensor as a fallback.
            }
        }

        foreach (var hw in _computer.Hardware)
        {
            hw.Update();
            foreach (var sub in hw.SubHardware)
                sub.Update();

            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    ReadCpu(hw, snap);
                    break;
                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    ReadGpu(hw, snap);
                    break;
                case HardwareType.Memory:
                    ReadMemory(hw, snap);
                    break;
                case HardwareType.Network:
                    ReadNetwork(hw, snap);
                    break;
            }
        }

        ReadPartitions(snap);
        return snap;
    }

    /// <summary>
    /// Prints every CPU sensor (name, type, value) for diagnosing which sensor
    /// actually reflects utilisation. Samples twice so values that need a delta
    /// between updates have a chance to settle.
    /// </summary>
    public void DumpCpuSensors(int samples = 3, int delayMs = 1000)
    {
        for (int i = 0; i < samples; i++)
        {
            if (i > 0) Thread.Sleep(delayMs);

            Console.WriteLine($"=== sample {i + 1} ===");
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType != HardwareType.Cpu) continue;
                hw.Update();
                foreach (var sub in hw.SubHardware) sub.Update();

                Console.WriteLine($"[{hw.HardwareType}] {hw.Name}");
                foreach (var sensor in hw.Sensors.OrderBy(s => s.SensorType).ThenBy(s => s.Name))
                {
                    Console.WriteLine(
                        $"  {sensor.SensorType,-12} | {sensor.Name,-34} | " +
                        $"{sensor.Value?.ToString("F2") ?? "null",8} | {sensor.Identifier}");
                }
            }
            Console.WriteLine();
        }
    }

    private void ReadCpu(IHardware hw, SystemSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is not float val) continue;
            switch (sensor.SensorType)
            {
                case SensorType.Temperature when sensor.Name.Contains("Package") || sensor.Name.Contains("Average"):
                    snap.CpuTempC = val;
                    break;
                // Fallback only: this LHM sensor is prone to reading ~100% when the power
                // plan's Maximum processor state is pinned at 100% (see SensorReader ctor).
                // It's only used if the "% Processor Time" counter above wasn't available.
                case SensorType.Load when sensor.Name.Contains("Total") && snap.CpuLoadPercent is null:
                    snap.CpuLoadPercent = val;
                    break;
                case SensorType.Power when sensor.Name.Contains("Package"):
                    snap.CpuPowerW = val;
                    break;
            }
        }
    }

    private void ReadGpu(IHardware hw, SystemSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is not float val) continue;
            switch (sensor.SensorType)
            {
                case SensorType.Temperature when sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"):
                    if (val < 150) snap.GpuTempC = val;
                    break;
                case SensorType.Load when sensor.Name.Contains("Core") || sensor.Name.Contains("GPU"):
                    snap.GpuLoadPercent = val;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("Memory Used"):
                    snap.GpuMemUsedMb = val;
                    break;
                case SensorType.SmallData when sensor.Name.Contains("Memory Total"):
                    snap.GpuMemTotalMb = val;
                    break;
                case SensorType.Power when sensor.Name.Contains("Package") || sensor.Name.Contains("GPU"):
                    snap.GpuPowerW = val;
                    break;
            }
        }
    }

    private void ReadMemory(IHardware hw, SystemSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is not float val) continue;
            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                snap.RamUsedGb = val;
        }
    }

    private void ReadNetwork(IHardware hw, SystemSnapshot snap)
    {
        foreach (var sensor in hw.Sensors)
        {
            if (sensor.Value is not float val) continue;
            if (sensor.SensorType == SensorType.Throughput)
            {
                if (sensor.Name.Contains("Upload"))
                    snap.NetUpBytesPerSec += (long)val;
                else if (sensor.Name.Contains("Download"))
                    snap.NetDownBytesPerSec += (long)val;
            }
        }
    }

    private void ReadPartitions(SystemSnapshot snap)
    {
        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                snap.Partitions.Add(new PartitionInfo(
                    drive.Name.TrimEnd('\\'),
                    drive.VolumeLabel,
                    drive.TotalSize / (1024.0 * 1024 * 1024),
                    (drive.TotalSize - drive.TotalFreeSpace) / (1024.0 * 1024 * 1024)
                ));
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _cpuLoadCounter?.Dispose();
        _computer.Close();
        GC.SuppressFinalize(this);
    }
}

public record PartitionInfo(string Letter, string Label, double TotalGb, double UsedGb);

public class SystemSnapshot
{
    public float? CpuTempC;
    public float? CpuLoadPercent;
    public float? CpuPowerW;

    public float? GpuTempC;
    public float? GpuLoadPercent;
    public float? GpuMemUsedMb;
    public float? GpuMemTotalMb;
    public float? GpuPowerW;

    public float? RamUsedGb;
    public float RamTotalGb; // actual installed RAM

    public List<PartitionInfo> Partitions = new();

    public long NetUpBytesPerSec;
    public long NetDownBytesPerSec;
}
