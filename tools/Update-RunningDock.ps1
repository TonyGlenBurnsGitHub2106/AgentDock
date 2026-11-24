# Brings the running Agent Dock up to date, the same as Settings -> Update Agent Dock but with no
# Yes / No (Story 6826). Meant for an agent that has just pushed a change to the dock: it asks the
# dock to update itself. The dock releases its panes alive, update.cmd pulls and builds, and the new
# dock takes the panes back. Open panes, the agent sessions in them included, keep running.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\Update-RunningDock.ps1
#
# An update is one sequence: every pane is released to the desktop alive, the dock closes, the
# code is pulled and built, and the new dock takes every terminal back in.
# Several docks running: every dock but one releases its panes and closes, then the last one
# updates, so the one new dock takes in all their terminals. A dock running as administrator is
# the one kept for the update, so the new dock can take in admin terminals too.
# No dock running: nothing to restart, so it only builds (build.cmd), and the next start is new.
# A dock older than 1.0.27 (1.0.25 for a single dock) does not know the request: update that one
# from its menu, once.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Add-Type -Namespace AgentDockTools -Name Win -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern int RegisterWindowMessage(string lpString);
[DllImport("user32.dll", SetLastError = true)]
public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
[DllImport("advapi32.dll", SetLastError = true)]
public static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);
[DllImport("advapi32.dll", SetLastError = true)]
public static extern bool GetTokenInformation(IntPtr token, int cls, out int info, int len, out int retLen);
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern bool QueryFullProcessImageName(IntPtr h, int flags, System.Text.StringBuilder name, ref int size);
[DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr h);
'@

# True when the process runs as administrator (token elevation, readable with limited rights).
function Test-Elevated([int]$id) {
    $h = [AgentDockTools.Win]::OpenProcess(0x1000, $false, $id)
    if ($h -eq [IntPtr]::Zero) { return $false }
    try {
        $tok = [IntPtr]::Zero
        if (-not [AgentDockTools.Win]::OpenProcessToken($h, 0x8, [ref]$tok)) { return $false }
        try {
            $val = 0; $len = 0
            if (-not [AgentDockTools.Win]::GetTokenInformation($tok, 20, [ref]$val, 4, [ref]$len)) { return $false }
            return $val -ne 0
        } finally { [AgentDockTools.Win]::CloseHandle($tok) | Out-Null }
    } finally { [AgentDockTools.Win]::CloseHandle($h) | Out-Null }
}

# The version of the exe a dock runs, read with limited rights so it works for a dock running as
# administrator too. 0.0 when it cannot be read.
function Get-DockVersion([int]$id) {
    $h = [AgentDockTools.Win]::OpenProcess(0x1000, $false, $id)
    if ($h -eq [IntPtr]::Zero) { return [version]'0.0' }
    try {
        $sb = New-Object System.Text.StringBuilder 1024; $n = $sb.Capacity
        if (-not [AgentDockTools.Win]::QueryFullProcessImageName($h, 0, $sb, [ref]$n)) { return [version]'0.0' }
        $v = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($sb.ToString()).FileVersion
        if ([string]::IsNullOrEmpty($v)) { return [version]'0.0' }
        return [version]$v
    } catch { return [version]'0.0' } finally { [AgentDockTools.Win]::CloseHandle($h) | Out-Null }
}

$canUpdate  = [version]'1.0.25'   # knows the update request
$canRelease = [version]'1.0.27'   # knows the release-only request

$docks = @(Get-Process -Name AgentDockStandalone -ErrorAction SilentlyContinue |
           Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
           Sort-Object StartTime)

if ($docks.Count -eq 0) {
    Write-Output "No Agent Dock is running. Building, so the next start is the new version."
    & cmd.exe /c "`"`"$root\build.cmd`" < nul`""
    if ($LASTEXITCODE -ne 0) { Write-Output "The build failed."; exit 1 }
    Write-Output "Built."
    exit 0
}

$msg = [AgentDockTools.Win]::RegisterWindowMessage('AgentDock.UpdateNow')

# The dock that updates: one running as administrator if any (it can take admin terminals back),
# else the newest. Every other dock releases its panes and closes first.
# Only a dock new enough to answer can be the one that updates. A dock too old for the request
# is left alone, never released into a gap with nobody to take its panes back.
$able = @($docks | Where-Object { (Get-DockVersion $_.Id) -ge $canUpdate })
if ($able.Count -eq 0) {
    Write-Output ("No running Agent Dock is new enough (1.0.25) to update on request: {0}. Update one from its menu, Settings -> Update Agent Dock, once." -f (($docks | ForEach-Object { $_.Id }) -join ', '))
    exit 4
}
$updater = @($able | Where-Object { Test-Elevated $_.Id } | Select-Object -Last 1)
if ($updater.Count -eq 0) { $updater = @($able | Select-Object -Last 1) }
$updater = $updater[0]

$stuck = @()
foreach ($d in $docks) {
    if ($d.Id -eq $updater.Id) { continue }
    if ((Get-DockVersion $d.Id) -lt $canRelease) { $stuck += $d.Id; continue }
    $ok = [AgentDockTools.Win]::PostMessage($d.MainWindowHandle, $msg, [IntPtr]1, [IntPtr]::Zero)
    if ($ok -and $d.WaitForExit(15000)) {
        Write-Output "Agent Dock $($d.Id) released its panes and closed."
    } else {
        $stuck += $d.Id
    }
}

if (-not [AgentDockTools.Win]::PostMessage($updater.MainWindowHandle, $msg, [IntPtr]::Zero, [IntPtr]::Zero)) {
    Write-Output "Could not reach the Agent Dock window (process id $($updater.Id))."
    exit 3
}

# The dock closes within a second or two once it hands off to update.cmd.
if ($updater.WaitForExit(15000)) {
    Write-Output "Agent Dock $($updater.Id) released its panes and closed. update.cmd is pulling, building and starting the new dock, which takes every terminal back in. Its window shows the progress."
    if ($stuck.Count -gt 0) {
        Write-Output ("Not part of it: Agent Dock {0}. It did not answer (older than 1.0.27, or a dialog is open), so its panes stay in it. Close it from its own menu." -f ($stuck -join ', '))
        exit 5
    }
    exit 0
}
Write-Output "The Agent Dock (process id $($updater.Id)) did not update. Either it is older than 1.0.25 and does not know the request (update it once from Settings -> Update Agent Dock), or one of its dialogs is open."
exit 4
