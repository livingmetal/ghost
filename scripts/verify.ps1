param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [ValidateSet("none", "win-x64", "win-arm64")]
    [string]$PublishRuntimeIdentifier = "none"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\LivingMetalGhost\LivingMetalGhost.csproj"
$publishScript = Join-Path $root "publish.ps1"
$fallbackDotnet = "C:\Users\livin\Documents\Codex\dotnet\dotnet.exe"

function Resolve-DotNet {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue

    if ($dotnetCommand) {
        return $dotnetCommand.Source
    }

    if (Test-Path $fallbackDotnet) {
        return $fallbackDotnet
    }

    throw "dotnet SDK was not found in PATH or fallback location. Install .NET 10 SDK or update scripts/verify.ps1."
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name"
    & $Action
    Write-Host "PASS: $Name"
}

if (-not (Test-Path $project)) {
    throw "Project file not found: $project"
}

$dotnet = Resolve-DotNet
Write-Host "Using dotnet: $dotnet"
Write-Host "Project: $project"
Write-Host "Configuration: $Configuration"

Invoke-Step "dotnet restore" {
    & $dotnet restore $project
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed."
    }
}

Invoke-Step "dotnet build" {
    & $dotnet build $project -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed."
    }
}

if ($PublishRuntimeIdentifier -ne "none") {
    if (-not (Test-Path $publishScript)) {
        throw "Publish script not found: $publishScript"
    }

    Invoke-Step "publish $PublishRuntimeIdentifier" {
        & powershell -ExecutionPolicy Bypass -File $publishScript -RuntimeIdentifier $PublishRuntimeIdentifier
        if ($LASTEXITCODE -ne 0) {
            throw "publish failed for $PublishRuntimeIdentifier."
        }
    }
}

Write-Host ""
Write-Host "Verification complete."
Write-Host "Evidence summary: restore passed, build passed$(if ($PublishRuntimeIdentifier -ne "none") { ", publish passed for $PublishRuntimeIdentifier" } else { "" })."
