$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "publish.ps1") -RuntimeIdentifier win-arm64
& (Join-Path $PSScriptRoot "publish.ps1") -RuntimeIdentifier win-x64

Write-Host "Published ARM64 and x64 executables."
