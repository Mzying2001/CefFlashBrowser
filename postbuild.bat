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

set "ASSETS_PATH=Assets"
set "CEF_PATH=%ASSETS_PATH%\CefSharp"
set "PLUGINS_PATH=%ASSETS_PATH%\Plugins"

if not exist "%CEF_PATH%\libcef_%CFB_BUILD_PLATFORM%.tar.gz" (
    echo Error: Missing "%CEF_PATH%\libcef_%CFB_BUILD_PLATFORM%.tar.gz". 1>&2
    exit /b 1
)
if not exist "%PLUGINS_PATH%\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz" (
    echo Error: Missing "%PLUGINS_PATH%\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz". 1>&2
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

rem Override the NuGet libcef.dll after the official CefSharp deployment.
tar -xvf "%CEF_PATH%\libcef_%CFB_BUILD_PLATFORM%.tar.gz" -C "%CEF_PATH%"
if errorlevel 1 exit /b 1

tar -xvf "%PLUGINS_PATH%\pepflashplayer_%CFB_BUILD_PLATFORM%.tar.gz" -C "%PLUGINS_PATH%"
if errorlevel 1 exit /b 1

del /q "%CEF_PATH%\libcef_*.tar.gz"
if errorlevel 1 exit /b 1

del /q "%PLUGINS_PATH%\pepflashplayer_*.tar.gz"
if errorlevel 1 exit /b 1

exit /b 0
