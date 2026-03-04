# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

The C++/CLI projects (CefFlashBrowser.Sol, CefFlashBrowser.Singleton) require Visual C++ build tools. There are no automated tests in this project.

## Architecture

**Solution:** `CefFlashBrowser.slnx` — 7 projects, MVVM pattern using SimpleMvvm framework.

### Projects

| Project | Type | Purpose |
|---------|------|---------|
| **CefFlashBrowser** | WPF App (.NET 4.6.2) | Main application — Views, ViewModels, Models, Utils |
| **CefFlashBrowser.FlashBrowser** | C# Library | Flash-enabled browser controls wrapping CefSharp |
| **CefFlashBrowser.WinformCefSharp4WPF** | C# Library | Bridges WinForms CefSharp into WPF via WindowsFormsHost |
| **CefFlashBrowser.Sol** | C++/CLI Library | SOL file parser/writer with AMF0/AMF3 serialization |
| **CefFlashBrowser.Singleton** | C++/CLI Library | Win32 IPC messaging for single-instance enforcement |
| **CefFlashBrowser.Log** | C# Library (.NET Standard 2.1) | File-based logging |
| **CefFlashBrowser.EmptyExe** | WPF Exe | Minimal subprocess used by CefSharp |

### Main App Structure (CefFlashBrowser/)

- **Models/Data/** — `GlobalData` (global app state, paths, config), `MessageTokens` (MVVM messenger tokens), `Settings` (serialized user preferences)
- **ViewModels/** — One ViewModel per window; `ViewModelLocator` wires DI via `SimpleIoc`
- **Views/** — WPF windows and dialogs; `Views/Custom/` for reusable controls; `Views/Dialogs/` for modal dialogs
- **Utils/** — Helpers (`WindowManager`, `ThemeManager`, `LanguageManager`, `DialogHelper`, `SolHelper`), `Converters/`, `Behaviors/`, `Handlers/`
- **Assets/** — Icons, SVGs, language resource dictionaries, bundled CEF/Flash binaries (tar.gz archives extracted at post-build)
- **Themes/** — WPF theme resource dictionaries

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
