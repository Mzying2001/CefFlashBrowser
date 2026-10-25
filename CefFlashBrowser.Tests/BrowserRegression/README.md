# Browser regression checks

These checks exercise the real WPF windows and CEF browser in an interactive Windows desktop session. They run separately from the MSTest unit tests and require Windows PowerShell 5.1 (Desktop edition).

## Build and run

Build the solution with Visual Studio MSBuild before running the checks. `dotnet build` cannot build the C++/CLI projects. Adjust the Visual Studio edition in the path if necessary.

Run these commands from the repository root in Command Prompt:

```cmd
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=C:\Program Files\dotnet
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" CefFlashBrowser.slnx -p:Configuration=Debug -p:Platform=x64 -restore
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\Run.ps1
```

For x86, build and run with the matching 32-bit PowerShell process:

```cmd
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" CefFlashBrowser.slnx -p:Configuration=Debug -p:Platform=x86 -restore
"%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\Run.ps1 -Platform x86
```

To test Release, build with `-p:Configuration=Release` and pass `-Configuration Release` to the runner. Defaults are `-Platform x64 -Configuration Debug`.

Always start a fresh process with `-STA`; the runner does not build the application or restart itself. Paths are resolved from `Run.ps1`, so an absolute script path also works from other working directories. The build outputs are `bin\x64\<Configuration>` for x64 and `bin\<Configuration>` for x86.

The runner exits with code 0 after all checks and cleanup succeed. An assertion, timeout, setup failure, or cleanup failure produces a nonzero exit code. Cleanup attempts to close every window, shut down CEF if it started, and dispose the logger. When a check fails, cleanup warnings preserve the original error.

## Files and execution order

| File | Responsibility |
| --- | --- |
| `Run.ps1` | Parameters, process checks, file loading, ordered execution, and final cleanup. |
| `Environment.ps1` | Run context, build output checks, assembly and WPF setup, fixtures, and CEF lifecycle. |
| `Helpers.ps1` | Assertions, Dispatcher waits, browser commands, window creation, JavaScript, DevTools, and reflection helpers. |
| `TestDoubles.cs` | Browser and popup proxies and the beforeunload handler, compiled at runtime with `Add-Type -Path`. Excluded from the MSTest project's compilation. |
| `Scenarios/BrowserWindow.ps1` | Template callbacks, DevTools, navigation and favorites, display state, closing, and saved state restoration. |
| `Scenarios/BrowserTabs.ps1` | Tab strip buttons, middle click, keyboard cycling, overflow, titles and fullscreen; foreground/background new-page routing; retained browser/DevTools state; source-specific callbacks; close cancellation and retry; sequential window close; and forced disposal of initializing tabs. |
| `Scenarios/ContextMenu.ps1` | Search and selected-URL routing for every new-page setting, explicit new-tab/new-window commands, menu composition and translations, closed-browser callbacks, and auxiliary-window routing. |
| `Scenarios/AuxiliaryWindows.ps1` | Popup, SWF player, and source windows: bindings, routing, DevTools, beforeunload, and cleanup. |
| `Scenarios/Lifecycle.ps1` | Operations before CEF initialization, navigation before native hosting, closing before Show or during initialization, and the close-all message. |

Only `Run.ps1` executes the suite. The supporting PowerShell files define functions without starting WPF or CEF when loaded. Functions receive a run context explicitly when they need shared resources; individual windows and browsers remain local to their scenarios.

The order is intentional: prepare WPF; check deferred browsers and tab management before CEF initialization; start CEF; check navigation before native hosting; close auxiliary windows before Show; exercise the main window and restore its saved state; close initializing main windows; exercise multiple tabs and context menus; exercise auxiliary windows; send close-all to windows with ready and initializing tabs; clean up. CEF starts once, and the checks run serially. Settings such as zoom and DevTools width carry across checks that verify persistence, so keep those checks together and in order.

## Test data and diagnostics

Each run creates a directory named `%TEMP%\CefFlashBrowser-regression-<guid>` for CEF cache, HTML fixtures, `app.log`, and `cef.log`. Settings and favorites are supplied in memory instead of loading the user's profile. The directory is retained, and its path is printed as soon as it is created so diagnostics remain available even if CEF stops responding. Process and missing-build checks can fail before any directory is created.

When adding checks, put the scenario in the relevant scenario file and register any new top-level scenario explicitly in `Run.ps1`. Put reusable browser operations in `Helpers.ps1`, and keep resource ownership and initialization in `Environment.ps1`.
