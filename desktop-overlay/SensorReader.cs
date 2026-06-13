using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace DesktopStats;

public class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private readonly float _installedRamGb;

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
    }

    public SystemSnapshot Read()
    {
        var snap = new SystemSnapshot();
        snap.RamTotalGb = _installedRamGb;

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
                case SensorType.Load when sensor.Name.Contains("Total"):
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
