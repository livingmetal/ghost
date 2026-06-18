param(
    [ValidateSet("win-arm64", "win-x64")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "src\LivingMetalGhost\LivingMetalGhost.csproj"
$dist = Join-Path $PSScriptRoot "dist"
$stage = Join-Path $PSScriptRoot "tmp\publish-$RuntimeIdentifier"
$outputName = if ($RuntimeIdentifier -eq "win-x64") {
    "LivingMetalGhost.exe"
} else {
    "LivingMetalGhost-$RuntimeIdentifier.exe"
}
$output = Join-Path $dist $outputName

function Resolve-DotNet {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        return $dotnetCommand.Source
    }

    if (-not [string]::IsNullOrWhiteSpace($env:LIVINGMETAL_DOTNET)) {
        $configuredDotnet = [Environment]::ExpandEnvironmentVariables($env:LIVINGMETAL_DOTNET)
        if (Test-Path -LiteralPath $configuredDotnet) {
            return (Resolve-Path -LiteralPath $configuredDotnet).Path
        }

        throw "LIVINGMETAL_DOTNET is set but the dotnet executable was not found: $configuredDotnet"
    }

    throw "dotnet SDK was not found in PATH. Install the .NET 10 SDK or set LIVINGMETAL_DOTNET to the dotnet executable path."
}

$dotnet = Resolve-DotNet
Write-Host "Using dotnet: $dotnet"

$iconScript = Join-Path $PSScriptRoot "tools\make-icon.ps1"
$iconSource = Join-Path $PSScriptRoot "src\LivingMetalGhost\Assets\Characters\ssyong\sleep.png"
$iconOutput = Join-Path $PSScriptRoot "src\LivingMetalGhost\Assets\App\LivingMetalGhost.ico"

if ((Test-Path -LiteralPath $iconScript) -and (Test-Path -LiteralPath $iconSource)) {
    powershell -NoProfile -ExecutionPolicy Bypass -File $iconScript -SourcePng $iconSource -OutputIco $iconOutput
    if ($LASTEXITCODE -ne 0) {
        throw "Icon generation failed."
    }
} else {
    Write-Warning "Ssyong icon generation skipped. Missing script or source sprite."
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
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Force
}
Copy-Item -LiteralPath (Join-Path $stage "LivingMetalGhost.exe") -Destination $output -Force
Remove-Item -LiteralPath $stage -Recurse -Force

# Explorer can keep the old icon for the same exe path. Ask Windows to refresh the icon cache lightly.
$ie4uinit = Join-Path $env:WINDIR "System32\ie4uinit.exe"
if (Test-Path -LiteralPath $ie4uinit) {
    & $ie4uinit -show | Out-Null
}

Write-Host "Published to $output"
Write-Host "If Explorer still shows the old icon, rename the exe once or restart Explorer; Windows may still be serving its icon cache."
