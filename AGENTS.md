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

```powershell
# Build Release (x86 + x64) and create versioned zip archives in bin\Publish\
powershell -ExecutionPolicy Bypass -File publish.ps1
```

## Architecture

**Solution:** `CefFlashBrowser.slnx` — 8 projects, MVVM pattern using SimpleMvvm framework.

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
| **CefFlashBrowser.Tests** | MSTest (.NET 4.6.2) | Unit tests — links source files from main app via `<Compile Include>` |

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

### Unit Tests (CefFlashBrowser.Tests/)

- **Framework:** MSTest (`Microsoft.NET.Test.Sdk`, `MSTest.TestAdapter`, `MSTest.TestFramework`)
- **Project references:** Only `CefFlashBrowser.Log` and `CefFlashBrowser.Sol` — does **not** reference the main app project
- **Source linking:** Testable source files (Models, Utils, Converters) are linked from the main app via `<Compile Include>` entries in the test `.csproj`. To test a new class, add a corresponding `<Compile Include>` link.
- **Limitation:** WPF is enabled (`<UseWPF>true`), but there is no reference to the main app project or CefSharp — cannot directly test code that depends on CefSharp or main app types not linked via `<Compile Include>`
- **Test data:** SOL fixture files in `TestData/` directory (copied to output via `<Content>`)
- **Test classes:** `AmfEncodingTests`, `SolFileReadWriteTests`, `SettingsTests`, `UrlHelperTests`, `ConverterTests`, `FileLoggerTests`

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
