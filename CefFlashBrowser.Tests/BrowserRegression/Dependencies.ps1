#Requires -Version 5.1
#Requires -PSEdition Desktop
[CmdletBinding()]
param(
    [ValidateSet('x64', 'x86')][string]$Platform = 'x64',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [string]$ApplicationPath
)

$ErrorActionPreference = 'Stop'
if (([IntPtr]::Size -eq 8) -ne ($Platform -eq 'x64')) {
    throw 'Use a fresh PowerShell process matching -Platform.'
}
if (!$ApplicationPath) {
    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $output = if ($Platform -eq 'x64') { "bin\x64\$Configuration" } else { "bin\$Configuration" }
    $ApplicationPath = Join-Path $root $output
}
$ApplicationPath = [IO.Path]::GetFullPath($ApplicationPath)
Add-Type -Path (Join-Path $PSScriptRoot 'AssemblyResolver.cs')
[RegressionAssemblyResolver]::Install($ApplicationPath)
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
[void][Reflection.Assembly]::LoadFrom((Join-Path $ApplicationPath 'Binaries\CefSharp\CefSharp.dll'))
$app = [Reflection.Assembly]::LoadFrom((Join-Path $ApplicationPath 'CefFlashBrowser.exe'))
[Runtime.CompilerServices.RuntimeHelpers]::RunModuleConstructor($app.ManifestModule.ModuleHandle)

$assemblies = @{}
foreach ($name in @('Mzying2001.MonkeySharp.Core', 'Mzying2001.MonkeySharp.CefSharp',
    'System.Text.Json', 'System.Text.Encoding.CodePages', 'ICSharpCode.AvalonEdit')) {
    $path = Join-Path $ApplicationPath ($name + '.dll')
    $assembly = if (Test-Path -LiteralPath $path) {
        [Reflection.Assembly]::LoadFrom($path)
    } else {
        [Reflection.Assembly]::Load($name)
    }
    $assemblies[$name] = $assembly
    if ($assembly.Location -and ![IO.Path]::GetFullPath($assembly.Location).StartsWith(
        $ApplicationPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Dependency resolved outside build output: $name"
    }
}
foreach ($name in @('Mzying2001.MonkeySharp.Core', 'Mzying2001.MonkeySharp.CefSharp')) {
    if ($assemblies[$name].GetName().Version.ToString() -ne '5.0.0.0') {
        throw "Unexpected MonkeySharp version: $name"
    }
}
$adapter = $assemblies['Mzying2001.MonkeySharp.CefSharp']
$cefReference = $adapter.GetReferencedAssemblies() | Where-Object Name -eq 'CefSharp'
if ($cefReference.Version.ToString() -ne '87.1.132.0') { throw 'The adapter must target CefSharp 87.1.132.' }
$parser = New-Object Mzying2001.MonkeySharp.Core.Parsing.UserScriptMetadataParser
$source = "// ==UserScript==`n// @name Dependency check`n// @match https://example.invalid/*`n// @grant none`n// ==/UserScript==`n"
if (!$parser.Parse($source).CanEnable) { throw 'Metadata parser could not load.' }
$value = [Mzying2001.MonkeySharp.Core.Storage.UserScriptValue]::FromJson('{"check":[true,null,1]}')
if (!$value.EncodedValue.StartsWith('o')) { throw 'Typed JSON dependencies could not load.' }
$resources = $assemblies['Mzying2001.MonkeySharp.Core'].GetManifestResourceNames()
if (@($resources | Where-Object { $_ -like '*.js' }).Count -eq 0 -or
    @($resources | Where-Object { $_ -like '*ApiCatalog.json' }).Count -ne 1) {
    throw 'MonkeySharp embedded bridge resources are missing.'
}
[void](New-Object Mzying2001.MonkeySharp.CefSharp.CefSharpHostOptions)
$editor = New-Object CefFlashBrowser.Views.Custom.ScriptEditor
$editor.SourceText = 'const answer = 42;'
if ($editor.Text -ne $editor.SourceText -or $editor.SyntaxHighlighting.Name -ne 'JavaScript') {
    throw 'AvalonEdit or its embedded JavaScript definition could not load.'
}
if ($Configuration -eq 'Release' -and $assemblies['ICSharpCode.AvalonEdit'].Location) {
    throw 'Release AvalonEdit must load from Costura resources.'
}
Write-Host "Runtime dependency check passed ($Configuration|$Platform)."
