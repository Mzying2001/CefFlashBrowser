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
:: Build and run unit tests
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=C:\Program Files\dotnet
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -restore
dotnet test CefFlashBrowser.Tests/CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build
```

```cmd
:: Build Release (x86 + x64) and create versioned zip archives in bin\Publish\
publish
```

## Project Structure

- **CefFlashBrowser/** — Main WPF application.
  - **Data/** — Shared application state, paths, configuration, and MVVM messaging data.
  - **Models/** — Domain models and persisted user settings.
  - **ViewModels/** — MVVM presentation logic for windows and dialogs.
  - **Views/** — WPF windows, dialogs, and reusable UI controls.
  - **Utils/** — Application helpers, converters, behaviors, and handlers.
  - **Assets/** — Icons, localization resources, and bundled runtime assets.
  - **Themes/** — WPF theme resource dictionaries.
- **CefFlashBrowser.FlashBrowser/** — Flash-enabled CefSharp browser control library.
- **CefFlashBrowser.WinformCefSharp4WPF/** — WPF hosting bridge for WinForms-based CefSharp controls.
- **CefFlashBrowser.Sol/** — C++/CLI library for SOL and AMF serialization.
- **CefFlashBrowser.Singleton/** — C++/CLI library for single-instance and IPC support.
- **CefFlashBrowser.Log/** — Shared file logging library.
- **CefFlashBrowser.EmptyExe/** — Minimal subprocess executable used by CefSharp.
- **CefFlashBrowser.Tests/** — MSTest project for linked application code and supporting libraries.
- **Docs/** — Reference specifications for AMF and SOL-related implementation work.

## Platform Notes

- Builds target both x86 and x64; output paths differ: `bin\Release\` (x86) vs `bin\x64\Release\` (x64)
- Entry point is `Program.cs` (not the default `App.xaml.cs`)
- CefSharp version 84.4.10 is pinned for Flash compatibility
- UI controls from HandyControl library

## Git Commit Conventions

- Use Conventional Commits: `<type>(<scope>): <summary>`
- Example types: `feat`, `fix`, `refactor`, `build`, `docs`, `test`, `chore`
- Use an optional scope for the affected project, area, class, or file
- Write concise English summaries
- For non-trivial changes, include a body explaining the reason and implementation/fix approach
