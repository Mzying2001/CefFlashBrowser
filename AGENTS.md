# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project Overview

CefFlashBrowser is a Windows WPF desktop application providing a web browser with built-in Flash Player support. It can browse the web, play local SWF files, and edit Flash game save data (SOL files).

## Build Commands

```bash
# Build entire solution (x64 Release)
dotnet build CefFlashBrowser.slnx --configuration Release --arch x64

# Build entire solution (x86 Release)
dotnet build CefFlashBrowser.slnx --configuration Release --arch x86

# Build Debug
dotnet build CefFlashBrowser.slnx --configuration Debug --arch x64
```

The C++/CLI projects (CefFlashBrowser.Sol, CefFlashBrowser.Singleton) require Visual C++ build tools.

```bash
# Build and run unit tests (requires VS MSBuild for C++/CLI projects)
# Step 1: Build with VS MSBuild (sets DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR for .NET SDK resolution)
DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR="C:\Program Files\dotnet" ^
  "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" ^
  CefFlashBrowser.Tests/CefFlashBrowser.Tests.csproj -p:Configuration=Debug -p:Platform=x64 -restore

# Step 2: Run tests
dotnet test CefFlashBrowser.Tests/CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build

# Alternative: Build in Visual Studio first, then run tests
dotnet test CefFlashBrowser.Tests/CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build
```

## Architecture

**Solution:** `CefFlashBrowser.slnx` — 7 projects, MVVM pattern using SimpleMvvm framework.

### Projects

| Project | Type | Purpose |
|---------|------|---------|
| **CefFlashBrowser** | WPF App (.NET 4.6.2) | Main application — Views, ViewModels, Models, Utils |
| **CefFlashBrowser.FlashBrowser** | C# Library | Flash-enabled browser controls wrapping CefSharp |
| **CefFlashBrowser.WinformCefSharp4WPF** | C# Library | Bridges WinForms CefSharp into WPF via HwndHost |
| **CefFlashBrowser.Sol** | C++/CLI Library | SOL file parser/writer with AMF0/AMF3 serialization |
| **CefFlashBrowser.Singleton** | C++/CLI Library | Win32 IPC messaging for single-instance enforcement |
| **CefFlashBrowser.Log** | C# Library (.NET 4.6.2) | File-based logging |
| **CefFlashBrowser.EmptyExe** | WPF Exe | Minimal subprocess used by CefSharp |

### Main App Structure (CefFlashBrowser/)

- **Data/** — `GlobalData` (global app state, paths, config), `MessageTokens` (MVVM messenger tokens)
- **Models/** — `Settings` (serialized user preferences), domain models (search engines, themes, SOL types, etc.)
- **ViewModels/** — One ViewModel per window; `ViewModelLocator` wires DI via `SimpleIoc`
- **Views/** — WPF windows and dialogs; `Views/Custom/` for reusable controls; `Views/Dialogs/` for modal dialogs
- **Utils/** — Helpers (`WindowManager`, `ThemeManager`, `LanguageManager`, `DialogHelper`, `SolHelper`), `Converters/`, `Behaviors/`, `Handlers/`
- **Assets/** — Icons, SVGs, language resource dictionaries, bundled CEF/Flash binaries (tar.gz archives extracted at post-build)
- **Themes/** — WPF theme resource dictionaries

### Reference Documentation (Docs/)

- `amf0-file-format-specification.pdf` — AMF0 file format specification (used by CefFlashBrowser.Sol)
- `amf3-file-format-spec.pdf` — AMF3 file format specification (used by CefFlashBrowser.Sol)

### Key Patterns

- **MVVM messaging:** Cross-component communication uses `Messenger` with tokens defined in `MessageTokens.cs`
- **Assembly embedding:** Costura.Fody bundles managed DLLs into the main exe; native DLLs (Sol, Singleton) are excluded and shipped separately
- **Post-build scripts:** The main `.csproj` has extensive post-build steps that extract tar.gz CEF/Flash archives and organize output directories
- **Localization:** XAML resource dictionaries in `Assets/Language/`; managed by `LanguageManager`
- **User data:** Stored in `%USERPROFILE%\Documents\CefFlashBrowser\` (settings.json, favorites.json)

### Platform Notes

- Builds target both x86 and x64; output paths differ: `bin\Release\` (x86) vs `bin\x64\Release\` (x64)
- Entry point is `Program.cs` (not the default `App.xaml.cs`)
- CefSharp version 84.4.10 is pinned for Flash compatibility
- UI controls from HandyControl library
