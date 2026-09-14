<#
.SYNOPSIS
    Configures Tailscale on Windows to keep its tunnel up with no user logged in.

.DESCRIPTION
    On Windows, tailscaled.exe already runs as a LocalSystem service. The problem is
    that by default it reference-counts attached GUI frontends (tailscale-ipn.exe) and
    tears the tunnel down when the last one disconnects. That is why the tunnel dies at
    the login screen, and why a different Windows user logging in first produces an
    unauthenticated frontend.

    This script:
      1. Sets ForceDaemon (unattended mode) on the daemon        -> tunnel survives logout
      2. Locks it via machine policy                             -> admin users can't flip it off
      3. Disables auto-update (pref + policy)                    -> no unattended service restarts
      4. Moves the GUI shortcut from all-users Startup to one    -> other users never launch a
         specific user's Startup folder                             frontend that can hijack state
      5. Installs a SYSTEM-context watchdog scheduled task       -> self-heal if the daemon dies

    SAFETY: this script never calls "tailscale up". On a tagged or long-lived node, "up"
    can decide it needs reauthentication and drop into an interactive browser login. On a
    headless machine that is a lockout. Only "tailscale set" is used, which mutates prefs
    only and cannot trigger reauth.

    Node identity (node key, tags, profile) is never touched. No re-registration occurs.

.PARAMETER GuiUser
    Which account keeps the tray GUI at logon. Accepts "DOMAIN\User", "User", or a SID.
    Defaults to the interactive console user, falling back to the user running the script.
    Profile paths are resolved from the registry by SID, so accounts whose profile folder
    name differs from their username (renamed accounts) resolve correctly.

.PARAMETER NoGui
    Remove the all-users Startup shortcut without recreating it for anyone. No tray icon
    for any account. The tunnel is unaffected.

.PARAMETER SkipWatchdog
    Do not install the watchdog scheduled task.

.PARAMETER Rollback
    Undo everything this script does and return to stock behavior.

.EXAMPLE
    .\Set-TailscaleUnattended.ps1
.EXAMPLE
    .\Set-TailscaleUnattended.ps1 -GuiUser 'AI\Josh'
.EXAMPLE
    .\Set-TailscaleUnattended.ps1 -NoGui
.EXAMPLE
    .\Set-TailscaleUnattended.ps1 -Rollback

.NOTES
    Windows 10/11 or Server 2016+. Run from an ELEVATED PowerShell prompt.
    Key expiry cannot be changed from the client. Disable it in the admin console:
    Machines -> [device] -> ... -> Disable key expiry.
#>

[CmdletBinding()]
param(
    [string] $GuiUser,
    [switch] $NoGui,
    [switch] $SkipWatchdog,
    [switch] $Rollback
)

$ErrorActionPreference = 'Stop'

$PolicyKey    = 'HKLM:\SOFTWARE\Policies\Tailscale'
$WatchdogDir  = 'C:\ProgramData\Tailscale-Watchdog'
$WatchdogTask = 'Tailscale-Watchdog'
$AllUsersLnk  = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\StartUp\Tailscale.lnk'

$script:Fail = 0

function Say  { param($m,$c='Gray')   Write-Host $m -ForegroundColor $c }
function Head { param($m)             Write-Host "`n=== $m ===" -ForegroundColor Cyan }
function Ok   { param($m)             Write-Host "  [ OK ] $m" -ForegroundColor Green }
function Warn { param($m)             Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Bad  { param($m)             Write-Host "  [FAIL] $m" -ForegroundColor Red; $script:Fail++ }
function Die  { param($m)             Write-Host "`nABORT: $m" -ForegroundColor Red; exit 1 }

# --------------------------------------------------------------------------
# Helpers
# --------------------------------------------------------------------------

function Get-TailscaleExe {
    foreach ($p in @(
        'C:\Program Files\Tailscale\tailscale.exe',
        'C:\Program Files (x86)\Tailscale IPN\tailscale.exe',
        "$env:ProgramFiles\Tailscale\tailscale.exe"
    )) { if (Test-Path $p) { return $p } }

    $svc = Get-CimInstance Win32_Service -Filter "Name='Tailscale'" -ErrorAction SilentlyContinue
    if ($svc -and $svc.PathName) {
        $cand = Join-Path (Split-Path ($svc.PathName -replace '"','')) 'tailscale.exe'
        if (Test-Path $cand) { return $cand }
    }
    return $null
}

function Get-GuiIdentity {
    # Returns @{ Sid; Name; Startup } for the account that should keep the tray GUI.
    param([string] $Requested)

    $sid = $null; $name = $null

    if ($Requested) {
        if ($Requested -match '^S-1-') {
            $sid = $Requested
            try { $name = (New-Object Security.Principal.SecurityIdentifier $sid).Translate(
                          [Security.Principal.NTAccount]).Value } catch { $name = $Requested }
        } else {
            try {
                $nt   = New-Object Security.Principal.NTAccount $Requested
                $sid  = $nt.Translate([Security.Principal.SecurityIdentifier]).Value
                $name = $nt.Value
            } catch { Die "Could not resolve account '$Requested'." }
        }
    }

    # Prefer the interactive console user (owner of explorer.exe).
    if (-not $sid) {
        $exp = Get-CimInstance Win32_Process -Filter "Name='explorer.exe'" -ErrorAction SilentlyContinue |
               Select-Object -First 1
        if ($exp) {
            $o = Invoke-CimMethod -InputObject $exp -MethodName GetOwner -ErrorAction SilentlyContinue
            if ($o.User) {
                try {
                    $nt   = New-Object Security.Principal.NTAccount "$($o.Domain)\$($o.User)"
                    $sid  = $nt.Translate([Security.Principal.SecurityIdentifier]).Value
                    $name = $nt.Value
                } catch { }
            }
        }
    }

    # Fall back to whoever is running this script.
    if (-not $sid) {
        $cur  = [Security.Principal.WindowsIdentity]::GetCurrent()
        $sid  = $cur.User.Value
        $name = $cur.Name
    }

    if ($sid -in @('S-1-5-18','S-1-5-19','S-1-5-20')) {
        Die "Resolved GUI user is a service account ($name). Pass -GuiUser explicitly, or use -NoGui."
    }

    # Resolve the profile path by SID, not by guessing C:\Users\<name>.
    $pp = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\$sid" `
           -ErrorAction SilentlyContinue).ProfileImagePath
    if (-not $pp) { Die "No profile path found for $name ($sid)." }
    $pp = [Environment]::ExpandEnvironmentVariables($pp)

    [pscustomobject]@{
        Sid     = $sid
        Name    = $name
        Startup = Join-Path $pp 'AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup\Tailscale.lnk'
    }
}

function New-Shortcut {
    param($Path, $Target)
    New-Item -ItemType Directory -Path (Split-Path $Path) -Force | Out-Null
    $w = New-Object -ComObject WScript.Shell
    $s = $w.CreateShortcut($Path)
    $s.TargetPath       = $Target
    $s.WorkingDirectory = Split-Path $Target
    $s.Description      = 'Tailscale'
    $s.Save()
}

# --------------------------------------------------------------------------
# Preconditions
# --------------------------------------------------------------------------

Head 'Preconditions'

if (-not ([Security.Principal.WindowsPrincipal] `
          [Security.Principal.WindowsIdentity]::GetCurrent()
         ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Die 'Not elevated. Re-run from an elevated PowerShell prompt.'
}
Ok 'Elevated'

$ts = Get-TailscaleExe
if (-not $ts) { Die 'tailscale.exe not found. Is Tailscale installed?' }
Ok "Binary: $ts"

$svc = Get-Service -Name Tailscale -ErrorAction SilentlyContinue
if (-not $svc) { Die 'Tailscale service not found.' }
if ($svc.Status -ne 'Running') {
    Warn 'Service not running - starting'
    Start-Service Tailscale; Start-Sleep 10
}
Ok "Service: $((Get-Service Tailscale).Status)"

$prefsBefore = & $ts debug prefs 2>&1
if ($LASTEXITCODE -ne 0) {
    Die "LocalAPI unreachable (exit $LASTEXITCODE). Cannot apply prefs safely.`n$prefsBefore"
}
Ok 'LocalAPI reachable'

$st = & $ts status --json 2>$null | ConvertFrom-Json
if ($st) {
    Say ("  Host      : {0}" -f $st.Self.HostName)
    Say ("  TailscaleIP: {0}" -f $st.Self.TailscaleIPs[0])
    Say ("  Tags      : {0}" -f ($(if ($st.Self.Tags) { $st.Self.Tags -join ',' } else { '(none)' })))
    Say ("  State     : {0}" -f $st.BackendState)
    Say ("  KeyExpiry : {0}" -f ($(if ($st.Self.KeyExpiry) { $st.Self.KeyExpiry } else { 'disabled' })))
    if ($st.BackendState -eq 'NeedsLogin') {
        Die 'Node is in NeedsLogin. Authenticate it before hardening, or you will lock yourself out.'
    }
}

# --------------------------------------------------------------------------
# Rollback
# --------------------------------------------------------------------------

if ($Rollback) {
    Head 'ROLLBACK'

    & $ts set --unattended=false 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { Ok 'ForceDaemon disabled' } else { Bad 'Could not clear ForceDaemon' }

    & $ts set --auto-update=true 2>&1 | Out-Null

    foreach ($v in 'UnattendedMode','InstallUpdates') {
        Remove-ItemProperty -Path $PolicyKey -Name $v -ErrorAction SilentlyContinue
    }
    Ok 'Policy values removed'

    Unregister-ScheduledTask -TaskName $WatchdogTask -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item $WatchdogDir -Recurse -Force -ErrorAction SilentlyContinue
    Ok 'Watchdog removed'

    $gui = Split-Path $ts | Join-Path -ChildPath 'tailscale-ipn.exe'
    if ((Test-Path $gui) -and -not (Test-Path $AllUsersLnk)) {
        New-Shortcut -Path $AllUsersLnk -Target $gui
        Ok 'All-users Startup shortcut restored'
    }

    Say "`nRolled back. Reboot to confirm stock behavior." 'Yellow'
    exit 0
}

# --------------------------------------------------------------------------
# Backup
# --------------------------------------------------------------------------

Head 'Backup'
$bk = "C:\ts-backup-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
New-Item -ItemType Directory -Path $bk -Force | Out-Null
$prefsBefore | Out-File "$bk\prefs-before.json" -Encoding UTF8
if (Test-Path $AllUsersLnk) { Copy-Item $AllUsersLnk "$bk\Tailscale.lnk" -Force }
Ok "Saved to $bk"

# --------------------------------------------------------------------------
# 1-2. Unattended mode + auto-update
# --------------------------------------------------------------------------

Head '1. Daemon preferences'

& $ts set --unattended=true 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Die 'tailscale set --unattended=true failed.' }
if (& $ts debug prefs | Select-String '"ForceDaemon": true') {
    Ok 'ForceDaemon = true'
} else {
    Die 'ForceDaemon did not stick. Stopping before further changes.'
}

& $ts set --auto-update=false 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) { Ok 'Auto-update disabled' } else { Warn 'Could not disable auto-update' }

# --------------------------------------------------------------------------
# 3. Policy lock
# --------------------------------------------------------------------------

Head '2. Machine policy'
New-Item -Path $PolicyKey -Force | Out-Null
New-ItemProperty -Path $PolicyKey -Name UnattendedMode -PropertyType String -Value 'always' -Force | Out-Null
New-ItemProperty -Path $PolicyKey -Name InstallUpdates -PropertyType String -Value 'never'  -Force | Out-Null

$pol = Get-ItemProperty $PolicyKey
if ($pol.UnattendedMode -eq 'always') { Ok 'UnattendedMode = always' } else { Bad 'UnattendedMode not set' }
if ($pol.InstallUpdates -eq 'never')  { Ok 'InstallUpdates = never' }  else { Bad 'InstallUpdates not set' }

# --------------------------------------------------------------------------
# 4. GUI scoping
# --------------------------------------------------------------------------

Head '3. GUI shortcut scoping'

if ($NoGui) {
    if (Test-Path $AllUsersLnk) { Remove-Item $AllUsersLnk -Force }
    Ok 'All-users shortcut removed; no per-user shortcut created'
} else {
    $id = Get-GuiIdentity -Requested $GuiUser
    Say "  Target user: $($id.Name)"
    Say "  Startup    : $($id.Startup)"

    $guiExe = Join-Path (Split-Path $ts) 'tailscale-ipn.exe'

    if (Test-Path $AllUsersLnk) {
        Copy-Item $AllUsersLnk $id.Startup -Force
        Remove-Item $AllUsersLnk -Force
        Ok 'Moved all-users shortcut to per-user Startup'
    } elseif (-not (Test-Path $id.Startup)) {
        if (Test-Path $guiExe) { New-Shortcut -Path $id.Startup -Target $guiExe; Ok 'Created per-user shortcut' }
        else { Warn 'tailscale-ipn.exe not found; no shortcut created' }
    } else {
        Ok 'Already scoped (all-users absent, per-user present)'
    }

    if (Test-Path $AllUsersLnk) { Bad 'All-users shortcut still present' } else { Ok 'All-users Startup clean' }
}

# --------------------------------------------------------------------------
# 5. Watchdog
# --------------------------------------------------------------------------

if (-not $SkipWatchdog) {
    Head '4. Watchdog scheduled task'

    New-Item -ItemType Directory -Path $WatchdogDir -Force | Out-Null

    $watchdog = @'
$ErrorActionPreference = "SilentlyContinue"
$ts   = "C:\Program Files\Tailscale\tailscale.exe"
if (-not (Test-Path $ts)) { $ts = "C:\Program Files (x86)\Tailscale IPN\tailscale.exe" }
$dir  = "C:\ProgramData\Tailscale-Watchdog"
$log  = "$dir\watchdog.log"
$cool = "$dir\lastrestart.txt"
function Log($m){ "[$(Get-Date -f 'yyyy-MM-dd HH:mm:ss')] $m" | Out-File $log -Append -Encoding UTF8 }

# 1. Wait for the service (tolerates the boot race).
$ok = $false
for ($i=0; $i -lt 24; $i++) {
    if ((Get-Service Tailscale -EA SilentlyContinue).Status -eq "Running") { $ok = $true; break }
    Start-Sleep 5
}
if (-not $ok) { Log "SVC not running after 120s - starting"; Start-Service Tailscale; Start-Sleep 15 }

# 2. Wait for the LocalAPI.
$st = $null
for ($i=0; $i -lt 12; $i++) {
    $raw = & $ts status --json 2>$null
    if ($raw) { try { $st = $raw | ConvertFrom-Json } catch {} }
    if ($st) { break }
    Start-Sleep 5
}
if (-not $st) { Log "ERROR LocalAPI unreachable"; exit 1 }

# 3. Let transient boot states settle.
if ($st.BackendState -in @("Starting","NoState")) {
    for ($i=0; $i -lt 12; $i++) {
        Start-Sleep 5
        $st = & $ts status --json 2>$null | ConvertFrom-Json
        if ($st.BackendState -eq "Running") { break }
    }
}
$state = $st.BackendState

# 4. Re-assert ForceDaemon if something cleared it.
if (-not (& $ts debug prefs 2>$null | Select-String '"ForceDaemon": true')) {
    Log "ForceDaemon OFF - re-asserting"
    & $ts set --unattended=true 2>&1 | Out-Null
}

# 5. Act. NOTE: never calls "tailscale up" - that could trigger interactive reauth.
if ($state -eq "Running") {
    Log "OK state=Running online=$($st.Self.Online)"
} elseif ($state -eq "NeedsLogin") {
    Log "CRITICAL state=NeedsLogin - node requires reauth, MANUAL ACTION (not restarting)"
} else {
    $last = 0
    if (Test-Path $cool) { $last = [int64](Get-Content $cool -Raw).Trim() }
    $mins = ([datetime]::Now.Ticks - $last) / 600000000
    if ($mins -ge 15) {
        Log "state=$state - restarting service"
        [datetime]::Now.Ticks | Out-File $cool -Encoding UTF8
        Restart-Service Tailscale -Force
    } else {
        Log "state=$state - cooldown ($([math]::Round($mins))m), skipping"
    }
}

if ((Get-Item $log -EA SilentlyContinue).Length -gt 200KB) {
    (Get-Content $log -Tail 500) | Out-File $log -Encoding UTF8
}
'@
    $watchdog | Out-File "$WatchdogDir\watchdog.ps1" -Encoding UTF8
    Ok 'watchdog.ps1 written'

    Unregister-ScheduledTask -TaskName $WatchdogTask -Confirm:$false -ErrorAction SilentlyContinue

    $act = New-ScheduledTaskAction -Execute 'powershell.exe' `
           -Argument "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$WatchdogDir\watchdog.ps1`""
    $t1  = New-ScheduledTaskTrigger -AtStartup
    $t1.Delay = 'PT1M'
    $t2  = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(2) `
           -RepetitionInterval (New-TimeSpan -Minutes 5)
    $pr  = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $se  = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable `
           -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
           -ExecutionTimeLimit (New-TimeSpan -Minutes 10)

    Register-ScheduledTask -TaskName $WatchdogTask -Action $act -Trigger $t1,$t2 `
        -Principal $pr -Settings $se | Out-Null

    $task = Get-ScheduledTask -TaskName $WatchdogTask -ErrorAction SilentlyContinue
    if ($task) { Ok "Task registered (State: $($task.State))" } else { Bad 'Task registration failed' }

    Start-ScheduledTask -TaskName $WatchdogTask
    Start-Sleep 25
    if (Test-Path "$WatchdogDir\watchdog.log") {
        Ok 'Watchdog ran:'
        Get-Content "$WatchdogDir\watchdog.log" -Tail 3 | ForEach-Object { Say "         $_" }
    } else {
        Bad 'Watchdog produced no log'
    }
}

# --------------------------------------------------------------------------
# Verify
# --------------------------------------------------------------------------

Head 'Verification'

if (& $ts debug prefs | Select-String '"ForceDaemon": true') { Ok 'ForceDaemon = true' }
                                                        else { Bad 'ForceDaemon NOT set' }

$pol = Get-ItemProperty $PolicyKey -ErrorAction SilentlyContinue
if ($pol.UnattendedMode -eq 'always') { Ok 'Policy UnattendedMode = always' } else { Bad 'Policy missing' }

if (Test-Path $AllUsersLnk) { Bad 'All-users Startup shortcut still present' }
                       else { Ok 'All-users Startup shortcut absent' }

$svc = Get-CimInstance Win32_Service -Filter "Name='Tailscale'"
if ($svc.StartMode -eq 'Auto' -and $svc.StartName -eq 'LocalSystem') { Ok 'Service: Auto / LocalSystem' }
                                                                else { Bad "Service: $($svc.StartMode) / $($svc.StartName)" }

$fin = & $ts status --json 2>$null | ConvertFrom-Json
if ($fin.BackendState -eq 'Running') { Ok "Backend Running (online=$($fin.Self.Online))" }
                                else { Bad "Backend state: $($fin.BackendState)" }

if ($fin.Self.KeyExpiry) { Warn "Key expires $($fin.Self.KeyExpiry) - disable key expiry in the admin console" }
                    else { Ok 'Key expiry disabled' }

if ((Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power' `
     -EA SilentlyContinue).HiberbootEnabled -eq 1) {
    Warn 'Fast Startup is ON - shutdown resumes a hibernated kernel session; test that path before relying on it'
}

if ((Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon' `
     -EA SilentlyContinue).AutoAdminLogon -eq '1') {
    Warn 'AutoAdminLogon is ON - a user logs in automatically, so a reboot will not test the no-user case'
}

$bl = Get-BitLockerVolume -MountPoint C: -ErrorAction SilentlyContinue
if ($bl -and $bl.KeyProtector.KeyProtectorType -match 'Pin') {
    Warn 'BitLocker pre-boot PIN detected - this machine will halt at a PIN prompt with no network'
}

Head 'Result'
if ($script:Fail -eq 0) {
    Say 'PASS - Tailscale will hold the tunnel with no user logged in.' 'Green'
    Say "Backup: $bk"
    Say ''
    Say 'Verify without rebooting: kill tailscale-ipn.exe, wait 30s, confirm BackendState is still Running.'
    exit 0
} else {
    Say "$($script:Fail) check(s) FAILED - review the output above." 'Red'
    Say "Backup: $bk    Undo with: .\Set-TailscaleUnattended.ps1 -Rollback"
    exit 1
}