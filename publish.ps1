#Requires -Version 5.1

$ErrorActionPreference = "Stop"

# -- 1. Find MSBuild via vswhere -----------------------------------------
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    Write-Error "vswhere.exe not found. Please install Visual Studio."
    exit 1
}

$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
    -find "MSBuild\**\Bin\amd64\MSBuild.exe" | Select-Object -First 1

if (-not $msbuild) {
    Write-Error "MSBuild.exe not found. Please install the Visual Studio Build Tools."
    exit 1
}

Write-Host "MSBuild: $msbuild" -ForegroundColor Cyan

# -- 2. Extract version from csproj --------------------------------------
$csproj = "$PSScriptRoot\CefFlashBrowser\CefFlashBrowser.csproj"
[xml]$xml = Get-Content $csproj
$version = $xml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (-not $version) {
    Write-Error "Failed to read <Version> from $csproj"
    exit 1
}

Write-Host "Version: $version" -ForegroundColor Cyan

# -- 3. Set environment variable -----------------------------------------
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "C:\Program Files\dotnet"

# -- 4. Clean Release output folders -------------------------------------
$binDir = "$PSScriptRoot\bin"
$releaseX86 = "$binDir\Release"
$releaseX64 = "$binDir\x64\Release"

foreach ($dir in @($releaseX86, $releaseX64)) {
    if (Test-Path $dir) {
        Write-Host "Cleaning $dir ..." -ForegroundColor Yellow
        Remove-Item $dir -Recurse -Force
    }
}

# -- 5. Build Release (x86) ----------------------------------------------
$sln = "$PSScriptRoot\CefFlashBrowser.slnx"

Write-Host "`nBuilding Release|x86 ..." -ForegroundColor Green
& $msbuild $sln -p:Configuration=Release -p:Platform=x86 -restore -verbosity:minimal
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed (x86)."; exit 1 }

# -- 6. Build Release (x64) ----------------------------------------------
Write-Host "`nBuilding Release|x64 ..." -ForegroundColor Green
& $msbuild $sln -p:Configuration=Release -p:Platform=x64 -restore -verbosity:minimal
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed (x64)."; exit 1 }

# -- 7. Create Publish directory -----------------------------------------
$publishDir = "$binDir\Publish"
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

# -- 8. Create zip archives ----------------------------------------------
$archives = @(
    @{ Source = $releaseX86; Name = "FlashBrowser_x86_v$version.zip" }
    @{ Source = $releaseX64; Name = "FlashBrowser_x64_v$version.zip" }
)

foreach ($arch in $archives) {
    $dest = "$publishDir\$($arch.Name)"
    if (Test-Path $dest) { Remove-Item $dest -Force }
    Write-Host "Packing $($arch.Name) ..." -ForegroundColor Green
    Compress-Archive -Path "$($arch.Source)\*" -DestinationPath $dest -Force
}

Write-Host "`nDone! Archives saved to $publishDir" -ForegroundColor Cyan
