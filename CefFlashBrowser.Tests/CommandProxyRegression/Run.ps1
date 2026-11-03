#Requires -Version 5.1
#Requires -PSEdition Desktop
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$ProxyPath
)

$ErrorActionPreference = 'Stop'
if (!$ProxyPath) {
    $repositoryPath = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $buildPath = if ($Platform -eq 'x64') { "bin\x64\$Configuration" } else { "bin\$Configuration" }
    $ProxyPath = Join-Path $repositoryPath "$buildPath\Assets\CommandProxy\CefFlashBrowser.CommandProxy.exe"
}
$ProxyPath = (Resolve-Path -LiteralPath $ProxyPath).Path
$image = [IO.File]::ReadAllBytes($ProxyPath)
$peOffset = [BitConverter]::ToInt32($image, 0x3c)
$machine = [BitConverter]::ToUInt16($image, $peOffset + 4)
$expectedMachine = if ($Platform -eq 'x64') { 0x8664 } else { 0x14c }
if ($machine -ne $expectedMachine -or [BitConverter]::ToUInt16($image, $peOffset + 24 + 68) -ne 2) {
    throw 'Expected a Windows GUI executable matching -Platform.'
}
$dataDirectory = $peOffset + 24 + $(if ($Platform -eq 'x64') { 112 } else { 96 })
if ([BitConverter]::ToUInt32($image, $dataDirectory + 14 * 8) -ne 0) {
    throw 'The command proxy must be native (no CLR header).'
}

$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ('CefFlashBrowser-command-proxy-' + [Guid]::NewGuid())
$installationPath = Join-Path $temporaryPath ('installation with spaces ' + [char]0x4e2d + [char]0x6587)
[void][IO.Directory]::CreateDirectory($installationPath)
Write-Host "Isolated command proxy checks: $temporaryPath"
# Exercise Unicode and spaces in COMSPEC even when the normal build path is simple.
$proxyCopy = Join-Path $installationPath 'CefFlashBrowser.CommandProxy.exe'
Copy-Item -LiteralPath $ProxyPath -Destination $proxyCopy
$fixture = Join-Path $temporaryPath 'runner.exe'
$compiler = New-Object CodeDom.Compiler.CompilerParameters
$compiler.CompilerOptions = "/platform:$Platform"
$compiler.ReferencedAssemblies.AddRange(@('System.dll', 'System.Core.dll'))
$compiler.GenerateExecutable = $true
$compiler.OutputAssembly = $fixture
$provider = New-Object Microsoft.CSharp.CSharpCodeProvider
try {
    $result = $provider.CompileAssemblyFromFile($compiler, (Join-Path $PSScriptRoot 'Runner.cs'))
    if ($result.Errors.HasErrors) { throw ($result.Errors | Out-String) }
} finally {
    $provider.Dispose()
}
& $fixture test $proxyCopy $installationPath
if ($LASTEXITCODE -ne 0) {
    throw "Command proxy regression failed. Diagnostics retained at $temporaryPath"
}
Write-Host "Command proxy regression checks passed ($Configuration|$Platform)."
