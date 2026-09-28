# DesktopStats Overlay

A Windows overlay that draws live system stats on your desktop wallpaper, behind every window. It picks its colours from the wallpaper and repositions itself when the resolution changes, including over RDP.

The panel is drawn with real per-pixel transparency, so text and gauges have clean edges on light and dark wallpapers alike. It reads the part of the wallpaper actually behind it, then picks light or dark text and an accent colour. If that area is too mixed for either, as with a bright shape crossing a dark wallpaper, it adds a subtle translucent card behind itself. It re-themes as soon as you change wallpaper. It scales with Windows display scaling (100%, 125%, 150% and so on), so it's the same physical size and equally sharp at 1080p, 1440p or 4K. It's click-through, so desktop icons underneath stay usable.

**What it shows:** CPU and GPU load, temperature and power, VRAM, RAM, used space on each fixed drive, and network throughput.

---

## Quick start

1. Download **[`DesktopStats.exe`](https://github.com/J0sh0909/small-projects/raw/main/desktop-overlay/DesktopStats.exe)** from this folder. It's one self-contained file, about 64 MB, with no .NET install needed.
2. Double-click it and approve the UAC prompt. The overlay appears on the right of your primary monitor.
3. Optional: to start it automatically at logon, run this once from any terminal:

   ```
   DesktopStats.exe --install
   ```

To stop it, end `DesktopStats.exe` in Task Manager. To remove the autostart, run `DesktopStats.exe --uninstall`.

**Requirements:** Windows 10 or 11 (x64) and an administrator account. It needs admin because LibreHardwareMonitor loads a kernel driver to read CPU temperature and power.

> The exe is unsigned, so SmartScreen may warn on first run. Choose **More info**, then **Run anyway**, or build it yourself (see below).

---

## Where each number comes from

Every metric is read from the most accurate source available for it, and sensors are matched by exact name. The overlay samples once a second.

| Metric | Source | Notes |
|---|---|---|
| CPU load | `GetSystemTimes` (Win32) | Based on scheduled idle time, the same basis as the `% Processor Time` counter. It stays accurate when the power plan pins *Maximum processor state* at 100%, a case where LibreHardwareMonitor's own load sensor reads ~100% at idle. |
| CPU temp | LibreHardwareMonitor | Intel: `CPU Package`. AMD: `Core (Tdie)`, then `Core (Tctl/Tdie)`, with `Tctl` only as a last resort, since it carries a +10/20 °C offset on some Ryzen X chips. |
| CPU power | LibreHardwareMonitor | `CPU Package` (Intel) or `Package` (AMD). Multi-socket systems show the sum. |
| GPU load / temp / power | LibreHardwareMonitor | Only **one** GPU, the one with the most dedicated VRAM, so a discrete card wins over an iGPU. Temp is the core/edge sensor, not Hot Spot or Memory Junction. Intel integrated GPUs have no temperature sensor, so they show a dash. |
| VRAM | LibreHardwareMonitor | Driver-reported dedicated memory. Shared system memory is not counted. |
| RAM | `GlobalMemoryStatusEx` (Win32) | Physical RAM in use (total minus available), out of installed RAM. Page file and commit charge are excluded. |
| Storage | `DriveInfo` | Every fixed drive, in GiB like Explorer. |
| Network | Windows interface byte counters | Only adapters that carry a default route, so VPN tunnels (Tailscale, WireGuard) and Hyper-V/WSL virtual switches aren't counted twice. |

A metric that can't be read is drawn as a dash (`—`), not a wrong number.

**Why the CPU % differs from Task Manager:** on Windows 10 1903+ and Windows 11, Task Manager shows *% Processor Utility*, which scales by clock speed relative to base clock. That makes it read higher under turbo and lower when cores are clocked down. The overlay shows time-based utilisation, which is what most monitoring tools report.

**To check what it's reading on your machine**, run `DesktopStats.exe --dump-sensors`. It lists every CPU, GPU and memory sensor, marks the GPU the overlay chose, and prints the exact values the overlay would draw.

---

## Commands

| Command | What it does |
|---|---|
| `DesktopStats.exe` | Run the overlay |
| `DesktopStats.exe --install` | Install machine-wide and start at every administrator's logon |
| `DesktopStats.exe --uninstall` | Remove the logon task and installed files |
| `DesktopStats.exe --status` | Show whether it's installed and running |
| `DesktopStats.exe --dump-sensors` | Print all sensors plus the values the overlay would show |
| `DesktopStats.exe --help` | Usage |

| Option | Applies to | Effect |
|---|---|---|
| `--delay <seconds>` | `--install` | Wait this long after logon before starting. Default `10` |
| `--no-copy` | `--install` | Register the exe where it is instead of copying it to Program Files |
| `--keep-files` | `--uninstall` | Remove the task but leave the binaries |

> The exe is a GUI app, so command output opens in its own console window, which stays open until you press a key.

---

## Autostart: how `--install` works

The Startup folder and the registry Run key can't silently elevate an app, so they don't work for something that needs admin. A scheduled task can. `--install` (which elevates itself through UAC):

1. Copies the exe to `%ProgramFiles%\DesktopStats`, so the task keeps working after you delete the download.
2. Registers a logon task, **DesktopStats Overlay**. Its principal is the local Administrators group and its trigger is "any user", so it runs elevated for **any administrator** who signs in, in that person's own session.
3. Starts the overlay.

Both `--install` and `--uninstall` affect every user on the machine. Standard (non-admin) accounts don't get the overlay, because they have no elevation to give.

<details>
<summary><b>RDP and multiple sessions</b></summary>

The task runs one instance per session (`MultipleInstancesPolicy=Parallel`):

- **The same account reconnecting** is a reconnect, not a logon. The existing overlay just carries on.
- **A different account connecting** starts a new session, so that person gets their own overlay. The console session is disconnected, but its process keeps running.

A disconnected overlay releases its sensors and stops polling, then picks them back up on reconnect. Without that, two instances would share LibreHardwareMonitor's kernel driver, and whichever exited first would unload it for the other. Locking the workstation doesn't count as a disconnect.

</details>

<details>
<summary><b>Setting it up by hand in Task Scheduler instead</b></summary>

1. `Win + R`, then `taskschd.msc`, then **Create Task...** (not "Create Basic Task")
2. **General**: name `DesktopStats Overlay`. Click **Change User or Group...** and enter `Administrators`. Select **Run only when user is logged on** and check **Run with highest privileges**.
3. **Triggers**: **New...**, **At log on**, **Any user**, delay `10 seconds`