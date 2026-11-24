# Brings the running Agent Dock up to date, the same as Settings -> Update Agent Dock but with no
# Yes / No (Story 6826). Meant for an agent that has just pushed a change to the dock: it asks the
# dock to update itself. The dock releases its panes alive, update.cmd pulls and builds, and the new
# dock takes the panes back. Open panes, the agent sessions in them included, keep running.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\Update-RunningDock.ps1
#
# No dock running: nothing to restart, so it only builds (build.cmd), and the next start is new.
# More than one dock running: it does nothing and says so, since they share one exe.
# A dock older than 1.0.25 does not know the request: update that one from its menu, once.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Add-Type -Namespace AgentDockTools -Name Win -MemberDefinition @'
[DllImport("user32.dll", CharSet = CharSet.Unicode)]
public static extern int RegisterWindowMessage(string lpString);
[DllImport("user32.dll", SetLastError = true)]
public static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
'@

$docks = @(Get-Process -Name AgentDockStandalone -ErrorAction SilentlyContinue |
           Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero })

if ($docks.Count -eq 0) {
    Write-Output "No Agent Dock is running. Building, so the next start is the new version."
    & cmd.exe /c "`"`"$root\build.cmd`" < nul`""
    if ($LASTEXITCODE -ne 0) { Write-Output "The build failed."; exit 1 }
    Write-Output "Built."
    exit 0
}

if ($docks.Count -gt 1) {
    Write-Output ("{0} Agent Docks are running (process ids {1}). They share one exe, so none was updated." -f `
        $docks.Count, (($docks | ForEach-Object { $_.Id }) -join ', '))
    Write-Output "Close the extra one, then run this again."
    exit 2
}

$dock = $docks[0]
$msg  = [AgentDockTools.Win]::RegisterWindowMessage('AgentDock.UpdateNow')
if (-not [AgentDockTools.Win]::PostMessage($dock.MainWindowHandle, $msg, [IntPtr]::Zero, [IntPtr]::Zero)) {
    Write-Output "Could not reach the Agent Dock window (process id $($dock.Id))."
    exit 3
}

# The dock closes within a second or two once it hands off to update.cmd.
if ($dock.WaitForExit(15000)) {
    Write-Output "The Agent Dock closed and update.cmd is pulling, building and restarting it. Its window shows the progress."
    exit 0
}
Write-Output "The Agent Dock (process id $($dock.Id)) did not update. Either it is older than 1.0.25 and does not know the request (update it once from Settings -> Update Agent Dock), or one of its dialogs is open."
exit 4
