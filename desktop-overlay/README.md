# DesktopStats Overlay

A Windows overlay that draws live system stats on your desktop wallpaper, behind every window. It picks its colours from the wallpaper and repositions itself when the resolution changes, including over RDP.

The panel is drawn with real per-pixel transparency, so text and gauges have clean edges on light and dark wallpapers alike. It scales with Windows display scaling (100%, 125%, 150% and so on), so it's the same physical size and equally sharp at 1080p, 1440p or 4K. It's click-through, so desktop icons underneath stay usable.

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
4. **Actions**: **New...**, **Start a program**, full path to `DesktopStats.exe`
5. **Conditions**: uncheck **Start the task only if the computer is on AC power**
6. **Settings**: check **Allow task to be run on demand**, uncheck **Stop the task if it runs longer than...**, choose **Run a new instance in parallel**

Step 2 is what makes it work for everyone. A single-account principal with an "Any user" trigger does nothing when anybody else signs in.

A task created this way is only visible in an *elevated* Task Scheduler. `--install` also grants read access to authenticated users so the task shows up normally.

</details>

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Overlay doesn't appear | It must run as admin. If using the task, check **Run with highest privileges** is set. |
| CPU temp/power show `—` but GPU values work | The sensor driver was blocked. Microsoft Defender flags the WinRing0 driver used by LibreHardwareMonitor 0.9.4 as a vulnerable driver and can quarantine it. Check **Windows Security → Protection history** and allow it, or see *Known limitations*. |
| Numbers look wrong for your hardware | Run `--dump-sensors` and compare its sensor list with the table above. It shows exactly which sensor each value came from. |
| GPU temp shows `—` | That GPU exposes no usable temperature sensor. Intel integrated graphics never do. Run `--dump-sensors`: if the chosen GPU lists no `Temperature` line, there is nothing to read. |
| GPU load shows 0% at the desktop | Normal. An idle desktop barely touches the GPU, and a game or video brings it up. |
| GPU stats come from the wrong GPU | The GPU with the most VRAM is chosen. `--dump-sensors` shows which one was picked. |
| Overlay doesn't start for one account | That account isn't an administrator. |
| Task missing from Task Scheduler but overlay runs | Open Task Scheduler as administrator, or reinstall with `--install`. |
| Overlay appears above windows or flickers | Run `--uninstall`, then `--install` again, or restart the task. |
| UAC prompt at every logon | The task isn't set to **Run with highest privileges**. Reinstall with `--install`. |
| `--install` says access denied | You declined UAC. Run it again and approve. |

### Known limitations

- **Sensor driver.** LibreHardwareMonitor 0.9.4 ships the old WinRing0 driver, which Defender may block (see above). Versions 0.9.5+ replaced it with [PawnIO](https://pawnio.eu/), but that driver has to be installed separately (`winget install namazso.PawnIO`), so the project stays on 0.9.4 for now to keep it a single exe.
- **One monitor.** The overlay covers the primary monitor only.

---

## Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or newer.

```powershell
git clone https://github.com/J0sh0909/small-projects.git
cd small-projects\desktop-overlay
.\build.ps1
```

Output lands in `artifacts\dist\`, which is gitignored:

| File | What it is |
|---|---|
| `DesktopStats.exe` | Self-contained single file. This is the one committed to this folder. |
| `DesktopStats-portable\` | Framework-dependent build, ~1 MB, needs the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) |
| `DesktopStats-portable.zip` | The above, zipped |

Options: `-Runtime win-arm64` targets ARM devices, and `-SkipZip` skips the zip.

To update the committed exe, copy `artifacts\dist\DesktopStats.exe` over `desktop-overlay\DesktopStats.exe`.

---

## Project layout

| Path | What it is |
|---|---|
| `DesktopStats.exe` | Prebuilt self-contained exe (win-x64) |
| `Program.cs` | Entry point and command-line dispatch |
| `OverlayForm.cs` | The overlay window: per-pixel-alpha layered rendering, DPI scaling, wallpaper theming, z-order and session handling |
| `SensorReader.cs` | Metric sampling. The table above describes each source. |
| `Installer.cs` | `--install` / `--uninstall` / `--status`, registering the logon task via `schtasks` |
| `app.manifest` | Requests administrator elevation |
| `build.ps1` | Builds the release exe and portable build into `artifacts\dist\` |
| `Directory.Build.props` | Sends all build output to `artifacts\` instead of `bin\` and `obj\` |

## License

MIT, see [LICENSE](../LICENSE) at the repository root.