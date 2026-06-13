# DesktopStats Overlay

A lightweight Windows desktop overlay that displays real-time system stats (CPU, GPU, RAM, storage, network) rendered directly on the desktop wallpaper layer. Automatically adapts its color theme to your wallpaper and repositions itself when the screen is resized (including RDP sessions).

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Desktop Runtime)
- Administrator privileges (required by LibreHardwareMonitor for hardware sensor access)

## Build

```powershell
cd desktop-overlay
dotnet publish -c Release -r win-x64 --self-contained false -o publish\
```

The output executable will be at `publish\DesktopStats.exe`.

---

## Autostart on Login (Task Scheduler)

The app requires administrator privileges, so the **Startup folder and registry Run key methods will not work** — Windows won't elevate them silently. Task Scheduler is the correct approach: it can run a task elevated at logon with no UAC prompt.

### Steps

1. **Build the project** and note the full path to `DesktopStats.exe`
   (e.g. `C:\Users\Joshua\Documents\Scripts\desktop-overlay\publish\DesktopStats.exe`)

2. **Open Task Scheduler**
   Press `Win + R`, type `taskschd.msc`, press Enter.

3. **Create a new task**
   In the right panel click **"Create Task..."** (not "Create Basic Task").

4. **General tab**
   - Name: `DesktopStats Overlay`
   - Select **"Run only when user is logged on"**
   - Check **"Run with highest privileges"**
   - Configure for: `Windows 10` (or `Windows 11`)

5. **Triggers tab**
   - Click **New...**
   - Begin the task: **"At log on"**
   - Specific user: select your account
   - (Optional) Add a delay of `10 seconds` to let the desktop fully load first
   - Click OK

6. **Actions tab**
   - Click **New...**
   - Action: **"Start a program"**
   - Program/script: full path to `DesktopStats.exe`
     e.g. `C:\Users\Joshua\Documents\Scripts\desktop-overlay\publish\DesktopStats.exe`
   - Click OK

7. **Conditions tab**
   - Uncheck **"Start the task only if the computer is on AC power"**
     (if you use a laptop)

8. **Settings tab**
   - Check **"Allow task to be run on demand"**
   - Uncheck **"Stop the task if it runs longer than..."**
   - If the task is already running: select **"Do not start a new instance"**

9. Click **OK**. Windows may prompt for your password to confirm elevated scheduling.

### Test it

Right-click the task in Task Scheduler and choose **"Run"** — the overlay should appear on your desktop immediately without a UAC prompt.

### Alternative: PowerShell one-liner

Run this in an elevated PowerShell to register the task automatically (adjust the exe path):

```powershell
$exe = "C:\Users\Joshua\Documents\Scripts\desktop-overlay\publish\DesktopStats.exe"

$action  = New-ScheduledTaskAction -Execute $exe
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -RunLevel Highest -LogonType Interactive

Register-ScheduledTask -TaskName "DesktopStats Overlay" `
    -Action $action -Trigger $trigger `
    -Settings $settings -Principal $principal -Force
```

---

## Uninstall / Remove from Startup

To stop it from running at startup, open Task Scheduler, find **DesktopStats Overlay**, right-click and choose **Delete**.

Or via PowerShell:

```powershell
Unregister-ScheduledTask -TaskName "DesktopStats Overlay" -Confirm:$false
```

---

## Troubleshooting

| Problem | Cause | Fix |
|---|---|---|
| Overlay doesn't appear | Not running as admin | Ensure "Run with highest privileges" is set in the task |
| Sensors show `—` | LibreHardwareMonitor needs admin | Same as above |
| Overlay mispositioned after RDP resize | Fixed — handled via `DisplaySettingsChanged` event | Update to latest build |
| Overlay flickers or appears on top of windows | Z-order timer issue | Restart the task via Task Scheduler |
| UAC prompt appears at logon | Task not configured for elevated logon | Verify "Run with highest privileges" and logon type is "Interactive" |
