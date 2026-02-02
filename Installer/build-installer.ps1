param(
  [Parameter(Mandatory=$true)]
  [string]$Version
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$publishDir = Join-Path $repoRoot "publish\win-x64"

if (-not (Test-Path $publishDir)) {
  Write-Error "Publish directory not found: $publishDir"
  exit 1
}

# Find ISCC.exe
$possible = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $possible | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = "iscc" }

$issPath = Join-Path $scriptDir "OSMBScriptManager.iss"
if (-not (Test-Path $issPath)) {
  Write-Error "ISS file not found: $issPath"
  exit 1
}

Write-Output "Running Inno Setup compiler..."
& $iscc "/dMyAppVersion=$Version" $issPath

# Locate built installer executable and move to Installer/Output
$exeName = "OSMBScriptManager-Setup-$Version-win-x64.exe"
$found = Get-ChildItem -Path (Get-Location) -Filter $exeName -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if ($found) {
  $outDir = Join-Path $scriptDir "Output"
  New-Item -ItemType Directory -Path $outDir -Force | Out-Null
  Move-Item $found.FullName -Destination (Join-Path $outDir $found.Name) -Force
  Write-Output "Installer created: $(Join-Path $outDir $found.Name)"
} else {
  Write-Error "Installer exe not found after running ISCC. Check ISCC output for errors."
  exit 1
}
