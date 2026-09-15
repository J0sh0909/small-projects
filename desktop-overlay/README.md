# DesktopStats Overlay

A lightweight Windows desktop overlay that displays real-time system stats (CPU, GPU, RAM, storage, network) rendered directly on the desktop wallpaper layer. Automatically adapts its color theme to your wallpaper and repositions itself when the screen is resized (including RDP sessions).

## Requirements

- Windows 10/11 (x64)
- Administrator privileges, because LibreHardwareMonitor loads a kernel driver to read hardware sensors
- .NET 8 Desktop Runtime, **only** for the portable download; the standalone `.exe` bundles its own runtime

---

## Install

Two ways to get it. Pick one.

| | **Option A: Standalone exe** | **Option B: Portable binaries** |
|---|---|---|
| Download | `DesktopStats.exe` | `DesktopStats-portable.zip` |
| Size | ~64 MB | ~1.3 MB zipped |
| .NET 8 Runtime required | No, bundled | Yes |
| Files on disk | One | A folder of DLLs |

Both are attached to each `desktop-overlay-*` tag on the [Releases](https://github.com/J0sh0909/small-projects/releases) page.

### Option A: Standalone exe

One self-contained file. Nothing else to install.

1. Download `DesktopStats.exe` from [Releases](https://github.com/J0sh0909/small-projects/releases).
2. Double-click it and approve the UAC prompt. The overlay appears on your desktop.

> First launch takes a second or two longer than later ones, because a single-file build unpacks itself to a temp folder on first run.

### Option B: Portable binaries

Smaller download, but you need the runtime.

1. Install the [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) if you don't already have it.
2. Download `DesktopStats-portable.zip` from [Releases](https://github.com/J0sh0909/small-projects/releases) and extract it anywhere.
3. Right-click `DesktopStats.exe` and choose **Run as administrator**.

### Build from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or newer.

```powershell
git clone https://github.com/J0sh0909/small-projects.git
cd small-projects\desktop-overlay
.\build.ps1
```

This produces both distributables in `artifacts\dist\`:

```
artifacts\dist\DesktopStats.exe             Option A, self-contained single file
artifacts\dist\DesktopStats-portable\       Option B, framework-dependent binaries
artifacts\dist\DesktopStats-portable.zip    Option B, zipped for a release upload
```

All build output, compiler intermediates included, is confined to `artifacts\`, which is
gitignored. There is no `bin\` or `obj\` in the project folder.

`build.ps1 -Runtime win-arm64` targets ARM devices. `-SkipZip` skips the archive.

---

## Autostart on Login

The overlay requires administrator privileges, so the **Startup folder and registry Run key will not work**: Windows won't elevate either silently. A scheduled task will, and the exe registers one for you:

```
DesktopStats.exe --install
```

That does three things:

1. Copies itself to `%ProgramFiles%\DesktopStats`, so the task survives deleting your download and every administrator can reach it.
2. Registers a logon task named **DesktopStats Overlay** whose principal is the local Administrators group, with a logon trigger carrying no user id.
3. Starts the overlay.

It elevates itself via UAC, so you don't need an admin terminal.

**This is a machine-wide install.** The overlay starts for *any* administrator who signs in, running elevated in that person's own session, not as whoever installed it. Standard (non-administrator) users are not covered: `app.manifest` requires elevation, and a standard account has no elevation to give.

### Multiple sessions and RDP

The task is registered with `MultipleInstancesPolicy=Parallel`, so each session gets its own overlay. That matters when someone takes a machine over by RDP:

- **Same account reconnecting** is a session reconnect, not a logon. No trigger fires, no second instance; the overlay you already had is still there.
- **A different account connecting** starts a second session, so the logon trigger fires and that person gets their own overlay. The console session is disconnected but its process keeps running.

For that second case the overlay releases its sensors on `ConsoleDisconnect` / `RemoteDisconnect` and reacquires them on reconnect. Without that, two instances would both hold LibreHardwareMonitor's shared kernel driver, and whichever exited first would tear it down for the other, leaving the person actually at the screen with dead sensors. Standing down also stops a disconnected session polling hardware and reshuffling z-order for a desktop nobody is looking at.

Locking the workstation is *not* treated as a disconnect: the session is still current, and the overlay sits behind every window regardless.

The other two policies are wrong here. `IgnoreNew` would leave an RDP user with no overlay at all, and `StopExisting` would kill the console instance and never bring it back, since reconnecting doesn't fire a logon trigger.

To undo it, which likewise affects every user on the machine:

```
DesktopStats.exe --uninstall
```

### All commands

| Command | What it does |
|---|---|
| `DesktopStats.exe` | Run the overlay |
| `DesktopStats.exe --install` | Install machine-wide, start at any administrator's logon |
| `DesktopStats.exe --uninstall` | Remove the logon task and installed files, for all users |
| `DesktopStats.exe --status` | Show whether it's installed and running |
| `DesktopStats.exe --dump-sensors` | Print every CPU sensor, for diagnostics |
| `DesktopStats.exe --help` | Usage |

`--install` options:

| Option | Effect |
|---|---|
| `--delay <seconds>` | Wait this long after logon before starting. Default 10 |
| `--no-copy` | Register the exe where it already is; don't copy it to Program Files. Warns if the path is inside a user profile, since other administrators may not be able to read it |

`--uninstall` takes `--keep-files` to remove the task but leave the binaries.

> Because the overlay is a GUI app, running it from a console pops a separate window for command output rather than printing inline. That window stays open until you press a key.

### Manual alternative (Task Scheduler GUI)

If you'd rather not let the exe register anything, `--install` is equivalent to:

1. `Win + R`, then `taskschd.msc`, then **Create Task...** (not "Create Basic Task")
2. **General**: name it `DesktopStats Overlay`, **Change User or Group...** and enter `Administrators`, select **Run only when user is logged on**, check **Run with highest privileges**
3. **Triggers**, **New...**, **At log on**, **Any user**, delay `10 seconds`
4. **Actions**, **New...**, **Start a program**, full path to `DesktopStats.exe`
5. **Conditions**: uncheck **Start the task only if the computer is on AC power** (laptops)
6. **Settings**: check **Allow task to be run on demand**, uncheck **Stop the task if it runs longer than...**, set **Run a new instance in parallel**

Step 2 is the part that makes it apply to everyone. Leaving the principal as a single account while setting the trigger to "Any user" produces a task that silently does nothing when anybody else signs in, because it still needs that one account's session.

Two things `--install` handles that the GUI route does not:

- A task built this way is readable only by the *unfiltered* Administrators token, so it vanishes from Task Scheduler whenever you open it without elevating, even as an administrator. `--install` grants authenticated users read access so it stays visible.
- Set **Run a new instance in parallel** in step 6, or an RDP user taking over from the console gets no overlay.

---

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| Overlay doesn't appear | Not running as admin | Ensure "Run with highest privileges" is set on the task |
| Sensor values render as a dash instead of a number | LibreHardwareMonitor needs admin | Same as above |
| `You must install .NET Desktop Runtime` on launch | Portable build without the runtime | Install the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), or use the standalone exe |
| `--install` says access denied | UAC declined | Approve the prompt, or run it from an elevated terminal |
| Overlay doesn't start for a particular account | That account isn't an administrator | Add it to the Administrators group; the app cannot read sensors without elevation |
| Task is missing from Task Scheduler, but the overlay is running | A hand-made group-principal task is readable only by the *unfiltered* Administrators token, so it is invisible in an unelevated Task Scheduler even to an admin | Reopen Task Scheduler as administrator, or let `--install` create the task: it grants authenticated users read access so the task shows up normally |
| SmartScreen warns about the exe | Unsigned binary | Choose **More info**, then **Run anyway**, or build from source |
| Overlay mispositioned after RDP resize | Fixed, handled via `DisplaySettingsChanged` | Update to the latest release |
| Overlay flickers or appears on top of windows | Z-order timer issue | `DesktopStats.exe --uninstall` then `--install`, or restart the task |
| UAC prompt appears at logon | Task not configured for elevated logon | Verify "Run with highest privileges" and logon type "Interactive" |

---

## Project layout

| Path | What it is |
|---|---|
| `Program.cs` | Entry point and command-line dispatch |
| `OverlayForm.cs` | The overlay window: rendering, wallpaper theming, z-order and resize handling |
| `SensorReader.cs` | LibreHardwareMonitor and performance counter sensor sampling |
| `Installer.cs` | `--install` / `--uninstall` / `--status`; registers the logon task via `schtasks` |
| `app.manifest` | Requests administrator elevation |
| `build.ps1` | Maintainer only, builds both release artifacts into `artifacts\dist\` |
| `Directory.Build.props` | Redirects all build output into `artifacts\` instead of `bin\` and `obj\` |

## License

MIT, see [LICENSE](../LICENSE) at the repository root.
