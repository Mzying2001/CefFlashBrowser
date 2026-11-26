@echo off
setlocal EnableExtensions DisableDelayedExpansion

if not defined CFB_BUILD_CONFIGURATION (
    echo Error: CFB_BUILD_CONFIGURATION is required. 1>&2
    exit /b 1
)
if /i not "%CFB_BUILD_PLATFORM%"=="x86" if /i not "%CFB_BUILD_PLATFORM%"=="x64" (
    echo Error: CFB_BUILD_PLATFORM must be x86 or x64. 1>&2
    exit /b 1
)

set "ASSETS_PATH=%~dp0CefFlashBrowser\Assets"
set "CEF_PATH=Binaries\CefSharp"
set "CEF_ARCHIVE=CefSharp\libcef_%CFB_BUILD_PLATFORM%.tar.gz"
set "PLUGINS_PATH=Binaries\Plugins"
set "VC_RUNTIME_PATH=Binaries\VCRuntime"
set "VC_RUNTIME_ARCHIVE=VCRuntime\vcruntime_%CFB_BUILD_PLATFORM%.tar.gz"

set "SUBPROCESS_PROJECT_PATH=%~dp0CefFlashBrowser.BrowserSubprocess"
set "SUBPROCESS_EXE=%SUBPROCESS_PROJECT_PATH%\bin\%CFB_BUILD_PLATFORM%\%CFB_BUILD_CONFIGURATION%\net462\CefFlashBrowser.BrowserSubprocess.exe"
set "CORE_JS_LICENSE=%SUBPROCESS_PROJECT_PATH%\Polyfills\LICENSE.core-js.txt"

rem Keep this list in sync with the CEF 87.1.6 Windows redistribution.
set "CEF_RUNTIME_FILES=libcef.dll chrome_elf.dll icudtl.dat snapshot_blob.bin v8_context_snapshot.bin cef.pak cef_100_percent.pak cef_200_percent.pak cef_extensions.pak devtools_resources.pak d3dcompiler_47.dll libEGL.dll libGLESv2.dll swiftshader\libEGL.dll swiftshader\libGLESv2.dll LICENSE.txt README.txt"
set "CEF_LOCALES=am ar bg bn ca cs da de el en-GB en-US es-419 es et fa fi fil fr gu he hi hr hu id it ja kn ko lt lv ml mr ms nb nl pl pt-BR pt-PT ro ru sk sl sr sv sw ta te th tr uk vi zh-CN zh-TW"

set "VC_RUNTIME_FILES=msvcp140.dll vcruntime140.dll"
if /i "%CFB_BUILD_PLATFORM%"=="x64" set "VC_RUNTIME_FILES=%VC_RUNTIME_FILES% vcruntime140_1.dll"

if not exist "%ASSETS_PATH%\%CEF_ARCHIVE%" (
    echo Error: Missing "%ASSETS_PATH%\%CEF_ARCHIVE%". 1>&2
    exit /b 1
)
if not exist "%ASSETS_PATH%\Plugins\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz" (
    echo Error: Missing "%ASSETS_PATH%\Plugins\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz". 1>&2
    exit /b 1
)
if not exist "%ASSETS_PATH%\%VC_RUNTIME_ARCHIVE%" (
    echo Error: Missing "%ASSETS_PATH%\%VC_RUNTIME_ARCHIVE%". 1>&2
    exit /b 1
)
if not exist "%SUBPROCESS_EXE%" (
    echo Error: Missing "%SUBPROCESS_EXE%". 1>&2
    exit /b 1
)
if not exist "%CORE_JS_LICENSE%" (
    echo Error: Missing "%CORE_JS_LICENSE%". 1>&2
    exit /b 1
)

if /i "%CFB_BUILD_CONFIGURATION%"=="Release" (
    for %%F in ("*.pdb" "*.xml" "%CEF_PATH%\*.pdb" "%CEF_PATH%\*.xml") do (
        if exist "%%~F" (
            del /q "%%~F"
            if errorlevel 1 exit /b 1
        )
    )
)

for %%D in ("%ASSETS_PATH%\.postbuild") do (
    set "EXTRACT_ROOT=%%~fD"
)

:PrepareExtraction
set "EXTRACT_NAME=%CFB_BUILD_PLATFORM%-%CFB_BUILD_CONFIGURATION%-%RANDOM%-%RANDOM%"
set "EXTRACT_PATH=%EXTRACT_ROOT%\%EXTRACT_NAME%"

rem Only create and recursively remove a direct child of the source extraction root.
for %%D in ("%EXTRACT_PATH%\..") do (
    if /i not "%%~fD"=="%EXTRACT_ROOT%" exit /b 1
)

if exist "%EXTRACT_PATH%" goto PrepareExtraction
set "RESULT=1"

mkdir "%EXTRACT_PATH%\CefSharp" "%EXTRACT_PATH%\Plugins" "%EXTRACT_PATH%\VCRuntime"
if errorlevel 1 goto Cleanup

call :Extract "%CEF_ARCHIVE%" ".postbuild\%EXTRACT_NAME%\CefSharp"
if errorlevel 1 goto Cleanup

call :Extract "Plugins\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz" ".postbuild\%EXTRACT_NAME%\Plugins"
if errorlevel 1 goto Cleanup

call :Extract "%VC_RUNTIME_ARCHIVE%" ".postbuild\%EXTRACT_NAME%\VCRuntime"
if errorlevel 1 goto Cleanup

for %%F in (%VC_RUNTIME_FILES%) do (
    if not exist "%EXTRACT_PATH%\VCRuntime\%%F" (
        echo Error: Missing "%%F" in "%VC_RUNTIME_ARCHIVE%". 1>&2
        goto Cleanup
    )
)

for %%F in (%CEF_RUNTIME_FILES%) do (
    if not exist "%EXTRACT_PATH%\CefSharp\%%F" (
        echo Error: Missing "%%F" in "%CEF_ARCHIVE%". 1>&2
        goto Cleanup
    )
)

for %%L in (%CEF_LOCALES%) do (
    if not exist "%EXTRACT_PATH%\CefSharp\locales\%%L.pak" (
        echo Error: Missing "locales\%%L.pak" in "%CEF_ARCHIVE%". 1>&2
        goto Cleanup
    )
)

if not exist "%CEF_PATH%\locales\" mkdir "%CEF_PATH%\locales"
if errorlevel 1 goto Cleanup

if not exist "%CEF_PATH%\swiftshader\" mkdir "%CEF_PATH%\swiftshader"
if errorlevel 1 goto Cleanup

if not exist "%PLUGINS_PATH%\" mkdir "%PLUGINS_PATH%"
if errorlevel 1 goto Cleanup

if not exist "%VC_RUNTIME_PATH%\" mkdir "%VC_RUNTIME_PATH%"
if errorlevel 1 goto Cleanup

rem Replace NuGet's CEF runtime with the complete set that matches the custom libcef.dll.
for %%F in (%CEF_RUNTIME_FILES%) do (
    copy /b /y "%EXTRACT_PATH%\CefSharp\%%F" "%CEF_PATH%\%%F"
    if errorlevel 1 goto Cleanup
)

copy /b /y "%EXTRACT_PATH%\CefSharp\locales\*.pak" "%CEF_PATH%\locales\"
if errorlevel 1 goto Cleanup

copy /b /y "%EXTRACT_PATH%\Plugins\pepflashplayer.dll" "%PLUGINS_PATH%\pepflashplayer.dll"
if errorlevel 1 goto Cleanup

copy /b /y "%ASSETS_PATH%\Plugins\readme.txt" "%PLUGINS_PATH%\readme.txt"
if errorlevel 1 goto Cleanup

rem CefSharp.BrowserSubprocess starts with its own DLL search directories.
for %%F in (%VC_RUNTIME_FILES%) do (
    copy /b /y "%EXTRACT_PATH%\VCRuntime\%%F" "%VC_RUNTIME_PATH%\%%F"
    if errorlevel 1 goto Cleanup
    copy /b /y "%EXTRACT_PATH%\VCRuntime\%%F" "%CEF_PATH%\%%F"
    if errorlevel 1 goto Cleanup
)

rem Replace the NuGet launcher with the custom renderer subprocess.
copy /b /y "%SUBPROCESS_EXE%" "%CEF_PATH%\CefSharp.BrowserSubprocess.exe"
if errorlevel 1 goto Cleanup

copy /b /y "%CORE_JS_LICENSE%" "%CEF_PATH%\LICENSE.core-js.txt"
if errorlevel 1 goto Cleanup

set "RESULT=0"

:Cleanup
if exist "%EXTRACT_PATH%\" (
    rmdir /s /q "%EXTRACT_PATH%"
    if errorlevel 1 exit /b 1
)
exit /b %RESULT%

:Extract
rem Relative paths also work with GNU tar, which treats drive letters as remote hosts.
pushd "%ASSETS_PATH%"
if errorlevel 1 exit /b 1
tar -xf "%~1" -C "%~2"
set "EXTRACT_RESULT=%ERRORLEVEL%"
popd

exit /b %EXTRACT_RESULT%
