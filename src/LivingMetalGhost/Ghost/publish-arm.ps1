param(
    [ValidateSet("win-arm64", "win-x64")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\\LivingMetalGhost\\LivingMetalGhost.csproj"
$dist = Join-Path $PSScriptRoot "dist"
$stage = Join-Path $PSScriptRoot "tmp\\publish-$RuntimeIdentifier"
$outputName = if ($RuntimeIdentifier -eq "win-x64") {
    "LivingMetalGhost.exe"
} else {
    "LivingMetalGhost-$RuntimeIdentifier.exe"
}
$output = Join-Path $dist $outputName
$fallbackDotnet = "C:\Users\livin\Documents\Codex\dotnet\dotnet.exe"
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = $null

if ($dotnetCommand) {
    $dotnet = $dotnetCommand.Source
}

if (-not $dotnet -and (Test-Path $fallbackDotnet)) {
    $dotnet = $fallbackDotnet
}

if (-not $dotnet) {
    throw "dotnet SDK was not found in PATH or fallback location."
}

if (Test-Path $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

& $dotnet publish $project `
  -c Release `
  -r $RuntimeIdentifier `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:IncludeAllContentForSelfExtract=true `
  -p:PublishTrimmed=false `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o $stage

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed for $RuntimeIdentifier."
}

New-Item -ItemType Directory -Path $dist -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $stage "LivingMetalGhost.exe") -Destination $output -Force
Remove-Item -LiteralPath $stage -Recurse -Force

Write-Host "Published to $output"
