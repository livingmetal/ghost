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
# Windows PowerShell 5.1 cannot parse double quotes nested inside $(...) inside a
# double-quoted string, so build the optional publish note as a separate variable.
$publishNote = if ($PublishRuntimeIdentifier -ne "none") { ", publish passed for $PublishRuntimeIdentifier" } else { "" }
Write-Host "Evidence summary: restore passed, build passed$publishNote."
