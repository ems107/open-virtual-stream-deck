# Builds the web client and publishes OVSD as a self-contained single-file Windows executable.
#   .\scripts\publish.ps1                -> dist\OVSD.exe
#   .\scripts\publish.ps1 -Output C:\OVSD
param(
    [string]$Output = (Join-Path $PSScriptRoot '..\dist'),
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

Write-Host '==> Building web client'
Push-Location (Join-Path $root 'web')
try {
    if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE) { throw 'npm ci failed' } }
    npm run build
    if ($LASTEXITCODE) { throw 'web build failed' }
}
finally { Pop-Location }

Write-Host '==> Publishing server'
dotnet publish (Join-Path $root 'server\src\OVSD.Host') `
    -c Release -r $Runtime --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $Output
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

Write-Host "==> Done: $(Join-Path (Resolve-Path $Output) 'OVSD.exe')"
