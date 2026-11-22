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
set "PLUGINS_PATH=Binaries\Plugins"
set "VC_RUNTIME_PATH=Binaries\VCRuntime"
set "VC_RUNTIME_ARCHIVE=VCRuntime\vcruntime_%CFB_BUILD_PLATFORM%.tar.gz"

set "VC_RUNTIME_FILES=msvcp140.dll vcruntime140.dll"
if /i "%CFB_BUILD_PLATFORM%"=="x64" set "VC_RUNTIME_FILES=%VC_RUNTIME_FILES% vcruntime140_1.dll"

if not exist "%ASSETS_PATH%\CefSharp\libcef_%CFB_BUILD_PLATFORM%.tar.gz" (
    echo Error: Missing "%ASSETS_PATH%\CefSharp\libcef_%CFB_BUILD_PLATFORM%.tar.gz". 1>&2
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

call :Extract "CefSharp\libcef_%CFB_BUILD_PLATFORM%.tar.gz" ".postbuild\%EXTRACT_NAME%\CefSharp"
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

if not exist "%CEF_PATH%\" mkdir "%CEF_PATH%"
if errorlevel 1 goto Cleanup

if not exist "%PLUGINS_PATH%\" mkdir "%PLUGINS_PATH%"
if errorlevel 1 goto Cleanup

if not exist "%VC_RUNTIME_PATH%\" mkdir "%VC_RUNTIME_PATH%"
if errorlevel 1 goto Cleanup

rem Override the NuGet libcef.dll after the official CefSharp deployment.
copy /b /y "%EXTRACT_PATH%\CefSharp\libcef.dll" "%CEF_PATH%\libcef.dll"
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
