# Builds dist\OVSD.exe and the Windows installer dist\OVSD-Setup-<version>.exe.
#   .\scripts\build-installer.ps1             # publish + installer
#   .\scripts\build-installer.ps1 -SkipPublish
# Uses Inno Setup's ISCC.exe if installed; otherwise downloads the portable compiler (NuGet
# package Tools.InnoSetup) into .tools\ — nothing is installed on the machine.
param([switch]$SkipPublish)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$dist = Join-Path $root 'dist'

[xml]$csproj = Get-Content (Join-Path $root 'server\src\OVSD.Host\OVSD.Host.csproj')
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'Version not found in OVSD.Host.csproj' }

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot 'publish.ps1') -Output $dist
}

$iscc = @(
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Join-Path $root '.tools\innosetup\tools\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Host '==> Downloading portable Inno Setup compiler'
    $tools = Join-Path $root '.tools'
    New-Item -ItemType Directory -Force $tools | Out-Null
    $latest = (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/tools.innosetup/index.json').versions[-1]
    $package = Join-Path $tools 'innosetup.zip'
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/tools.innosetup/$latest/tools.innosetup.$latest.nupkg" -OutFile $package
    Expand-Archive $package (Join-Path $tools 'innosetup') -Force
    Remove-Item $package
    $iscc = Join-Path $tools 'innosetup\tools\ISCC.exe'
}

Write-Host "==> Building installer $version"
& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$dist" (Join-Path $root 'installer\ovsd.iss')
if ($LASTEXITCODE) { throw 'ISCC failed' }
Write-Host "==> Done: $(Join-Path $dist "OVSD-Setup-$version.exe")"
