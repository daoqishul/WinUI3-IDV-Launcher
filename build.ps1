param(
    [switch]$Package
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'winui\IdvLauncher.csproj'
$dist = Join-Path $root 'dist'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet SDK was not found. Install the .NET 8 SDK first.'
}

if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}

Write-Host 'Publishing WinUI 3 app...'
dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained false -o $dist
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Get-ChildItem $dist -Filter '*.pdb' -File | Remove-Item -Force -ErrorAction SilentlyContinue

$distReadme = Join-Path $root 'packaging\README.txt'
if (Test-Path $distReadme) {
    Copy-Item $distReadme (Join-Path $dist 'README.txt') -Force
}

if ($Package) {
    $zip = Join-Path $root 'IDVLauncher-WinUI3-v1.0.0-win64.zip'
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Packaged: $zip"
}

Write-Host "Output: $dist"
Get-ChildItem $dist -File | Select-Object Name, Length
