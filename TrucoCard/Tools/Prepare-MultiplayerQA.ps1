param([string]$Destination = 'D:\TrucoQA\ClientA')
$ErrorActionPreference = 'Stop'
$sourceProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$qaDestination = [System.IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $qaDestination) { throw "Destination already exists; choose a new QA directory." }
if ($qaDestination.StartsWith($sourceProject, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'QA clone must be outside the source project.'
}
New-Item -ItemType Directory -Path $qaDestination | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    & robocopy (Join-Path $sourceProject $folder) (Join-Path $qaDestination $folder) /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { throw "Clone failed for $folder ($LASTEXITCODE)" }
}
Write-Output "Copied working files to $qaDestination. Library and runtime PlayerPrefs were not copied; source configuration is preserved."
Write-Output 'Before launching: isolate productName, replace API URL with approved staging, and isolate Photon AppVersion.'
