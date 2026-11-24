# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project Overview

CefFlashBrowser is a Windows WPF desktop application providing a web browser with built-in Flash Player support. It can browse the web, play local SWF files, and edit Flash game save data (SOL files).

## Build Commands

The solution contains C++/CLI projects, so **must use VS MSBuild** (not `dotnet build`). The MSBuild path may vary by VS edition (Community/Professional/Enterprise).

```cmd
:: Build solution (x64 Debug)
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=C:\Program Files\dotnet
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  CefFlashBrowser.slnx -p:Configuration=Debug -p:Platform=x64 -restore
```

```cmd
:: Build unit test project only (x64 Debug)
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=C:\Program Files\dotnet
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -restore
```

```cmd
:: Build Release (x86 + x64) and create versioned zip archives in bin\Publish\
publish
```

## Test Commands

Build the required Debug|x64 outputs first. Unit tests require the test project; browser regression checks require the full solution.

```cmd
:: Run unit tests (x64 Debug)
dotnet test CefFlashBrowser.Tests/CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build
```

```cmd
:: Run browser regression checks (fresh x64 STA process)
powershell -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\Run.ps1
```

For browser regression setup, x86/Release commands, and diagnostics, see `CefFlashBrowser.Tests/BrowserRegression/README.md`.

## Project Structure

- **CefFlashBrowser/**: Main WPF application.
  - **Browser/**: CefSharp handlers, SWF playback, and per-browser userscript integration.
  - **Data/**: Shared application state, paths, configuration, and MVVM messaging data.
  - **Infrastructure/**: Reusable infrastructure helpers for collections and WPF integration.
  - **Models/**: Domain models and persisted user settings.
  - **Services/**: Application services for language, theme, windows, favorites, database access, and favicons.
    - **Downloads/**: Download management, transfers, and recovery.
    - **UserScripts/**: Userscript installation, updates, execution policy, and persistence.
  - **ViewModels/**: MVVM presentation logic for windows and dialogs.
  - **Views/**: WPF windows, dialogs, and reusable UI controls.
  - **Utils/**: Utility helpers and cross-cutting support code.
  - **Assets/**: Icons, localization resources, and bundled runtime assets.
  - **Themes/**: WPF theme resource dictionaries.
- **CefFlashBrowser.FlashBrowser/**: Flash-enabled CefSharp browser controls and page SWF discovery.
- **CefFlashBrowser.WinformCefSharp4WPF/**: WPF hosting bridge for WinForms-based CefSharp controls.
- **CefFlashBrowser.Sol/**: C++/CLI library for SOL and AMF serialization.
- **CefFlashBrowser.Singleton/**: C++/CLI library for single-instance and IPC support.
- **CefFlashBrowser.Database/**: Database interfaces and SQLite implementation.
- **CefFlashBrowser.Log/**: Shared file logging library.
- **CefFlashBrowser.CommandProxy/**: Native command proxy with Flash diagnostic filtering.
- **CefFlashBrowser.Tests/**: MSTest project for linked application code and supporting libraries.
  - **BrowserRegression/**: PowerShell checks for WPF/CEF behavior, userscripts, downloads, and packaged releases.
  - **CommandProxyRegression/**: Regression checks for command forwarding and diagnostic filtering.
  - **TestData/**: SOL fixtures for serialization tests.
- **ReleaseNotes/**: Python CLI for bilingual GitHub release notes.
- **Docs/**: Reference specifications for AMF and SOL-related implementation work.

## Notes

- Builds target both x86 and x64; output paths differ: `bin\Release\` (x86) vs `bin\x64\Release\` (x64)
- Entry point is `Program.cs` (not the default `App.xaml.cs`)
- CefSharp version 84.4.10 is pinned for Flash compatibility
- Follow the repository's `.editorconfig` when modifying or adding files
- Use English PascalCase for UIA control names unless a dynamic name is needed.

## Git Commit Conventions

- Use Conventional Commits: `<type>(<scope>): <summary>`, with concise English summaries.
- Optional scope: affected project, area, class, or file.
- For non-trivial changes, explain why and how in the body.
