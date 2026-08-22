# Fixes Unity Android build blocked by Smart App Control / Application Control (0x800711C7)
# Run elevated (UAC). Turns SAC Off + adds Defender exclusions for Unity.

$ErrorActionPreference = "Continue"
$unityRoot = "D:\Unity Game Engine\6000.0.80f1"
$androidPlayer = Join-Path $unityRoot "Editor\Data\PlaybackEngines\AndroidPlayer"
$exe = Join-Path $androidPlayer "AndroidPlayerBuildProgram.exe"

function Assert-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    $p = New-Object Security.Principal.WindowsPrincipal($id)
    if (-not $p.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Host "ERROR: Must run as Administrator." -ForegroundColor Red
        exit 1
    }
}

Assert-Admin
Write-Host "=== Fix Unity Android Application Control block ===" -ForegroundColor Cyan
Write-Host "Unity: $unityRoot"

# 1) Unblock files (Zone.Identifier)
Write-Host "`n[1/4] Unblocking AndroidPlayer binaries..." -ForegroundColor Yellow
if (Test-Path $androidPlayer) {
    Get-ChildItem $androidPlayer -Recurse -Include *.exe,*.dll -ErrorAction SilentlyContinue | ForEach-Object {
        Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
        try { Remove-Item -Path $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue } catch {}
    }
    Write-Host "Unblock complete."
} else {
    Write-Host "WARNING: AndroidPlayer folder missing: $androidPlayer" -ForegroundColor Red
}

# 2) Turn Smart App Control OFF (0=Off, 1=Evaluation, 2=On)
Write-Host "`n[2/4] Disabling Smart App Control..." -ForegroundColor Yellow
$sacKey = "HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy"
try {
    if (-not (Test-Path $sacKey)) { New-Item -Path $sacKey -Force | Out-Null }
    $before = (Get-ItemProperty $sacKey -ErrorAction SilentlyContinue).VerifiedAndReputablePolicyState
    Write-Host "SAC state before: $before (0=Off 1=Eval 2=On)"
    Set-ItemProperty -Path $sacKey -Name "VerifiedAndReputablePolicyState" -Value 0 -Type DWord -Force
    $after = (Get-ItemProperty $sacKey).VerifiedAndReputablePolicyState
    Write-Host "SAC state after:  $after"
    if ($after -ne 0) {
        Write-Host "Could not fully turn SAC off via registry. Use Windows Security UI." -ForegroundColor Yellow
    }
} catch {
    Write-Host "SAC registry change failed: $($_.Exception.Message)" -ForegroundColor Red
}

# 3) Defender exclusions
Write-Host "`n[3/4] Adding Windows Defender exclusions..." -ForegroundColor Yellow
try {
    Add-MpPreference -ExclusionPath $unityRoot -ErrorAction Stop
    Write-Host "ExclusionPath: $unityRoot"
} catch { Write-Host "ExclusionPath: $($_.Exception.Message)" -ForegroundColor Yellow }
try {
    Add-MpPreference -ExclusionPath $androidPlayer -ErrorAction Stop
} catch {}
try {
    Add-MpPreference -ExclusionProcess "AndroidPlayerBuildProgram.exe" -ErrorAction Stop
    Add-MpPreference -ExclusionProcess "Unity.exe" -ErrorAction Stop
    Add-MpPreference -ExclusionProcess "UnityCrashHandler64.exe" -ErrorAction Stop
    Write-Host "ExclusionProcess: AndroidPlayerBuildProgram.exe, Unity.exe"
} catch { Write-Host "ExclusionProcess: $($_.Exception.Message)" -ForegroundColor Yellow }

# 4) Verify launch
Write-Host "`n[4/4] Verifying AndroidPlayerBuildProgram.exe..." -ForegroundColor Yellow
if (-not (Test-Path $exe)) {
    Write-Host "MISSING: $exe" -ForegroundColor Red
    Write-Host "Reinstall Android Build Support from Unity Hub for 6000.0.80f1."
    exit 2
}
try {
    $p = Start-Process -FilePath $exe -ArgumentList "--version" -PassThru -Wait -WindowStyle Hidden -ErrorAction Stop
    Write-Host "LAUNCH OK (exit $($p.ExitCode)). Android builds should work after reboot if SAC was On." -ForegroundColor Green
} catch {
    Write-Host "STILL BLOCKED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Do this manually then REBOOT:" -ForegroundColor Yellow
    Write-Host "  Windows Security > App & browser control > Smart App Control > Off"
    Write-Host "  Then restart the PC and rebuild in Unity."
    exit 3
}

Write-Host ""
Write-Host "DONE. Close Unity fully, then rebuild Android." -ForegroundColor Green
Write-Host "If SAC was previously On, reboot once so policy reloads." -ForegroundColor Yellow
exit 0
