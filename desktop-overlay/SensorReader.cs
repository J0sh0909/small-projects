using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace DesktopStats;

/// <summary>
/// Samples every metric the overlay shows. Each one comes from whichever source reads it
/// most accurately, not from a single library:
///
///   CPU load     GetSystemTimes deltas (idle-thread accounting, same as "% Processor Time")
///   CPU temp/W   LibreHardwareMonitor, exact sensor names in priority order
///   GPU          LibreHardwareMonitor, one GPU only (the one with the most VRAM)
///   RAM          GlobalMemoryStatusEx (physical memory, not commit charge)
///   Network      Interface byte counters on adapters that carry a default route
///   Storage      DriveInfo
///
/// LibreHardwareMonitor is only opened for CPU and GPU. Memory, network, motherboard and
/// storage were previously enabled too, which meant polling SMART on every drive and every
/// Super I/O chip once a second for values that were either unused or less accurate than
/// the OS APIs below.
///
/// Sensors are matched by exact name, never by substring. Substring matching is what made
/// the old reader misread: "Memory Used" also matches "D3D Shared Memory Used" and
/// "Virtual Memory Used", "GPU" matches "GPU Hot Spot" and "GPU Memory Junction", and
/// whichever sensor LHM happened to list last won.
/// </summary>
public sealed class SensorReader : IDisposable
{
    private readonly Computer _computer;
    private readonly IHardware[] _cpus;
    private readonly IHardware? _gpu;
    private readonly float _installedRamGb;

    // CPU load state: previous GetSystemTimes sample.
    private long _prevCpuIdle, _prevCpuTotal;
    private float? _lastCpuLoad;

    // Network state.
    private readonly Stopwatch _netClock = Stopwatch.StartNew();
    private readonly Dictionary<string, (long Rx, long Tx)> _netPrev = new();
    private NetworkInterface[] _netInterfaces = Array.Empty<NetworkInterface>();
    private long _netLastTicks;
    private bool _netHasBaseline;
    private long _netLastRefreshTicks;
    private long _netDownRate, _netUpRate;
    private volatile bool _netDirty = true;

    private static readonly TimeSpan NetRefreshInterval = TimeSpan.FromSeconds(30);

    // ── Sensor names, most preferred first. Verified against LibreHardwareMonitorLib 0.9.4. ──

    // Intel: "CPU Package". AMD: Tdie is the real die temperature; Tctl on Zen/Zen+ X parts
    // carries a +10/+20 °C fan-curve offset, so it is only a last resort. "Core Average"
    // and "CCDs Average" read noticeably below what every other tool reports as CPU temp.
    private static readonly string[] CpuTempNames =
    {
        "CPU Package", "Core (Tdie)", "Core (Tctl/Tdie)", "CCDs Max (Tdie)", "Core Max",
        "Core (Tctl)", "Core Average", "CCDs Average (Tdie)",
    };

    // Intel: "CPU Package". AMD: "Package".
    private static readonly string[] CpuPowerNames = { "CPU Package", "Package" };

    // Fallback only; see ReadCpuLoad.
    private static readonly string[] CpuLoadNames = { "CPU Total" };

    // "GPU Core" on NVIDIA and AMD. Intel iGPUs have no core load sensor, only D3D engines.
    private static readonly string[] GpuLoadNames = { "GPU Core", "D3D 3D" };

    // Edge temperature. Hot Spot and Memory Junction run 10-20 °C hotter and are not what
    // any vendor overlay shows as "GPU temperature". Some GPUs (AMD APUs, some older AMD
    // cards) don't name their edge sensor "GPU Core"; ReadGpuTemp falls back to any
    // temperature that isn't one of the hotter secondary sensors below.
    private static readonly string[] GpuTempNames = { "GPU Core" };

    private static readonly string[] GpuSecondaryTempMarkers =
    {
        "Hot Spot", "Junction", "Memory", "VR ", "Liquid", "PLX", "Inlet", "Outlet", "Board",
    };

    // NVIDIA: "GPU Package" (board power). AMD: "GPU Package", else PPT, else core.
    // Intel iGPU: "GPU Power".
    private static readonly string[] GpuPowerNames = { "GPU Package", "GPU PPT", "GPU Power", "GPU Core" };

    // Used/total pairs, read together so both numbers come from the same source.
    // The driver-reported pair is preferred; the D3D dedicated pair covers GPUs without one.
    // D3D *Shared* memory is system RAM the GPU may borrow, and is deliberately never read.
    private static readonly (string Used, string Total)[] GpuMemoryNames =
    {
        ("GPU Memory Used", "GPU Memory Total"),
        ("D3D Dedicated Memory Used", "D3D Dedicated Memory Total"),
    };

    // ── Win32 ──

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]