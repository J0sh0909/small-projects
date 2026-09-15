# DesktopStats Overlay

A lightweight Windows desktop overlay that displays real-time system stats (CPU, GPU, RAM, storage, network) rendered directly on the desktop wallpaper layer. Automatically adapts its color theme to your wallpaper and repositions itself when the screen is resized (including RDP sessions).

## Requirements

- Windows 10/11 (x64)
- Administrator privileges — LibreHardwareMonitor loads a kernel driver to read hardware sensors
- .NET 8 Desktop Runtime — **only** for the portable download; the standalone `.exe` bundles its own runtime

---

## Install

Two ways to get it. Pick one.

| | **Option A — Standalone exe** | **Option B — Portable binaries** |
|---|---|---|
| Download | `DesktopStats.exe` | `DesktopStats-portable.zip` |
| Size | ~69 MB | ~1.3 MB zipped |
| .NET 8 Runtime required | No — bundled | Yes |
| Files on disk | One | A folder of DLLs |

Both are attached to each `desktop-overlay-*` tag on the [Releases](https://github.com/J0sh0909/small-projects/releases) page.

### Option A — Standalone exe

One self-contained file. Nothing else to install.

1. Download `DesktopStats.exe` from [Releases](https://github.com/J0sh0909/small-projects/releases).
2. Double-click it and approve the UAC prompt. The overlay appears on your desktop.

> First launch takes a second or two longer than later ones — a single-file build unpacks itself to a temp folder on first run.

### Option B — Portable binaries

Smaller download, but you need the runtime.

1. Install the [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) if you don't already have it.
2. Download `DesktopStats-portable.zip` from [Releases](https://github.com/J0sh0909/small-projects/releases) and extract it anywhere.
3. Right-click `DesktopStats.exe` → **Run as administrator**.

### Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or newer.

```powershell
git clone https://github.com/J0sh0909/small-projects.git
cd small-projects\desktop-overlay
.\build.ps1
```

This produces both distributables in `artifacts\dist\`:

```
artifacts\dist\DesktopStats.exe             Option A — self-contained single file
artifacts\dist\DesktopStats-portable\       Option B — framework-dependent binaries
artifacts\dist\DesktopStats-portable.zip    Option B, zipped for a release upload
```

All build output — compiler intermediates included — is confined to `artifacts\`, which is
gitignored. There is no `bin\` or `obj\` in the project folder.

`build.ps1 -Runtime win-arm64` targets ARM devices. `-SkipZip` skips the archive.

---

## Autostart on Login

The overlay requires administrator privileges, so the **Startup folder and registry Run key will not work** — Windows won't elevate either silently. A scheduled task will, and the exe registers one for you:

```
DesktopStats.exe --install
```

That copies itself to `%LOCALAPPDATA%\DesktopStats` (so the task survives deleting your download), registers a logon task named **DesktopStats Overlay** running elevated as the current user, and starts the overlay. It elevates itself via UAC — no need for an admin terminal.

To undo it:

```
DesktopStats.exe --uninstall
```

### All commands

| Command | What it does |
|---|---|
| `DesktopStats.exe` | Run the overlay |
| `DesktopStats.exe --install` | Install and start at every logon |
| `DesktopStats.exe --uninstall` | Remove the logon task and installed files |
| `DesktopStats.exe --status` | Show whether it's installed and running |
| `DesktopStats.exe --dump-sensors` | Print every CPU sensor, for diagnostics |
| `DesktopStats.exe --help` | Usage |

`--install` options:

| Option | Effect |
|---|---|
| `--delay <seconds>` | Wait this long after logon before starting. Default 10 |
| `--no-copy` | Register the exe where it already is; don't copy it |
| `--user <DOMAIN\name>` | Install for this account instead of the current one. Only needed if you elevated as a different user |

`--uninstall` takes `--keep-files` to remove the task but leave the binaries.

> Because the overlay is a GUI app, running it from a console pops a separate window for command output rather than printing inline. That window stays open until you press a key.

### Manual alternative (Task Scheduler GUI)

If you'd rather not let the exe register anything, `--install` is equivalent to:

1. `Win + R` → `taskschd.msc` → **Create Task...** (not "Create Basic Task")
2. **General**: name it `DesktopStats Overlay`, select **Run only when user is logged on**, check **Run with highest privileges**
3. **Triggers** → **New...** → **At log on**, your account, delay `10 seconds`
4. **Actions** → **New...** → **Start a program** → full path to `DesktopStats.exe`
5. **Conditions**: uncheck **Start the task only if the computer is on AC power** (laptops)
6. **Settings**: check **Allow task to be run on demand**, uncheck **Stop the task if it runs longer than...**, set **Do not start a new instance**

---

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| Overlay doesn't appear | Not running as admin | Ensure "Run with highest privileges" is set on the task |
| Sensors show `—` | LibreHardwareMonitor needs admin | Same as above |
| `You must install .NET Desktop Runtime` on launch | Portable build without the runtime | Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), or use the standalone exe |
| `--install` says access denied | UAC declined | Approve the prompt, or run it from an elevated terminal |
| SmartScreen warns about the exe | Unsigned binary | **More info** → **Run anyway**, or build from source |
| Overlay mispositioned after RDP resize | Fixed — handled via `DisplaySettingsChanged` | Update to the latest release |
| Overlay flickers or appears on top of windows | Z-order timer issue | `DesktopStats.exe --uninstall` then `--install`, or restart the task |
| UAC prompt appears at logon | Task not configured for elevated logon | Verify "Run with highest privileges" and logon type "Interactive" |

---

## Project layout

| Path | What it is |
|---|---|
| `Program.cs` | Entry point and command-line dispatch |
| `OverlayForm.cs` | The overlay window — rendering, wallpaper theming, z-order and resize handling |
| `SensorReader.cs` | LibreHardwareMonitor + performance counter sensor sampling |
| `Installer.cs` | `--install` / `--uninstall` / `--status`; registers the logon task via `schtasks` |
| `app.manifest` | Requests administrator elevation |
| `build.ps1` | Maintainer only — builds both release artifacts into `artifacts\dist\` |
| `Directory.Build.props` | Redirects all build output into `artifacts\` instead of `bin\` + `obj\` |

## License

MIT — see [LICENSE](../LICENSE) at the repository root.
