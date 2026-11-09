# Browser regression checks

These checks exercise the real WPF windows and CEF browser in an interactive Windows desktop session. They run separately from the MSTest unit tests and require Windows PowerShell 5.1 (Desktop edition).

The native keyboard checks retry initial foreground activation within a bounded wait, then hold Ctrl while sending repeated W key presses. If WPF activation fails, setup briefly attaches to the foreground input queue while requesting native activation, then detaches before focusing the page or holding Ctrl. A competing window interrupts the first activation in one scenario to verify recovery. The checks verify the foreground window before each shortcut and release Ctrl during cleanup. Leave keyboard input to the runner while these checks execute.

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

After `publish`, validate each actual zip in a fresh process of the matching architecture:

```cmd
powershell -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\PackageSmoke.ps1 -ArchivePath bin\Publish\FlashBrowser_x64_v1.1.8.zip -Platform x64
"%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\PackageSmoke.ps1 -ArchivePath bin\Publish\FlashBrowser_x86_v1.1.8.zip -Platform x86
```

Adjust the version in the archive names when needed. The smoke check extracts only the supplied archive into an isolated temporary directory and initializes the executable's Costura resolver for embedded managed assemblies. It verifies that runtime dependencies are deployed under `Binaries\CefSharp`, `Binaries\CmdProxy`, `Binaries\SQLite`, and `Binaries\Plugins`, with no external `Assets` directory or dependency archives. It requires exactly one SQLite native library at `Binaries\SQLite\SQLite.Interop.dll` and verifies that the process loads that file while keeping the application's CEF DLL search path. It exercises the real IoC factories, SQLite persistence and shutdown, and checks that unregistering unused services neither creates a database nor loads SQLite. It also runs the [command proxy regression checks](../CommandProxyRegression/README.md) against the packaged native executable. It does not start CEF or request any network resources.

Always start a fresh process with `-STA`; the runner does not build the application or restart itself. Paths are resolved from `Run.ps1`, so an absolute script path also works from other working directories. The build outputs are `bin\x64\<Configuration>` for x64 and `bin\<Configuration>` for x86.

The runner exits with code 0 after all checks and cleanup succeed. An assertion, timeout, setup failure, or cleanup failure produces a nonzero exit code. Cleanup attempts to close every window, shut down CEF if it started, and dispose the logger. When a check fails, cleanup warnings preserve the original error.

## Files and execution order

| File | Responsibility |
| --- | --- |
| `Run.ps1` | Parameters, process checks, file loading, ordered execution, and final cleanup. |
| `Environment.ps1` | Run context, build output checks, assembly and WPF setup, fixtures, and CEF lifecycle. |
| `Helpers.ps1` | Assertions, Dispatcher waits, browser commands, window creation, JavaScript, DevTools, and reflection helpers. |
| `Scenarios/Language.ps1` | Automatic initialization, saved language selection, resource switching, settings and notifications, missing translations, invalid language fallback, injected logging, and language names and synchronized selection in the main menu and first-start dialog before CEF starts. |
| `Scenarios/Favorites.ps1` | Shared singleton collection, automatic loading, file compatibility, explicit saving, edits and ordering, malformed files, and failed writes before CEF starts. |
| `Scenarios/ProxySettings.ps1` | DPAPI password persistence, legacy plaintext cleanup during settings merge, settings UI password bindings, and password-free error logging before CEF starts. |
| `TestDoubles.cs` | Browser and popup proxies, native keyboard input and CEF audio-state helpers, and the beforeunload handler, compiled at runtime with `Add-Type -Path`. Excluded from the MSTest project's compilation. |
| `Scenarios/BrowserWindow.ps1` | Template callbacks, DevTools, navigation and favorites, display state, closing, and saved state restoration. |
| `FaviconHttpFixture.cs`, `Scenarios/Favicons.ps1` | Local HTTP pages, redirects and failed images; discovered mappings, all three favorites surfaces, editing, ordering, empty-state updates and saving on manager close; live refresh, image fallback, and queued navigation and disposal callbacks. |
| `Scenarios/BrowserTabControl.ps1` | Standalone BrowserTab with ordinary items and a read-only source before CEF starts: adaptive widths, scrolling, selection, drag dependency property, bubbling move requests handled by the consumer, control isolation, button gestures, and preview/timer cleanup on cancellation, removal, template replacement and unloading. |
| `Scenarios/BrowserTabs.ps1` | Tab strip buttons, middle click, keyboard cycling, drag reordering and edge scrolling, preview content for later and scrolled tabs, overflow, titles and fullscreen; localized tab menus with a separated final mute item; actions-menu mute after new window, shared-menu retargeting and background-tab isolation; native Ctrl+M from page and address-bar focus with shortcut hints; mute state checked against native CEF audio, repeated toggles and nonempty tab-icon drawings; duplication and new-window actions; sequential close-other-tabs cancellation, retry and interruption; foreground/background new-page routing; retained browser/DevTools state; source-specific callbacks; close cancellation and retry; sequential window close; and forced disposal of initializing tabs. |
| `Scenarios/ContextMenu.ps1` | Search and selected-URL routing for every new-page setting, explicit new-tab/new-window commands, page-menu composition without mute actions in either audio state, translations, closed-browser callbacks, and auxiliary-window routing. |
| `Scenarios/AuxiliaryWindows.ps1` | Popup, SWF player, and source windows: bindings, routing, DevTools, beforeunload, and cleanup; embedded SWF player reloads, local and network source assignment, and independent player windows without external HTML files. |
| `Scenarios/Lifecycle.ps1` | Operations before CEF initialization, navigation before native hosting, closing before Show or during initialization, and the close-all message. |

Only `Run.ps1` executes the suite. The supporting PowerShell files define functions without starting WPF or CEF when loaded. Functions receive a run context explicitly when they need shared resources; individual windows and browsers remain local to their scenarios.

The order is intentional: prepare WPF; verify language initialization and switching and favorites persistence; check deferred browsers, the standalone tab control and tab management before CEF initialization; start CEF; check navigation before native hosting; close auxiliary windows before Show; exercise the main window, favorites surfaces and favicons; restore saved state; close initializing main windows; exercise multiple tabs and context menus; exercise auxiliary windows; send close-all to windows with ready and initializing tabs; clean up. CEF starts once, and the checks run serially. Settings such as zoom and DevTools width carry across checks that verify persistence, so keep those checks together and in order.

## Test data and diagnostics

Each run creates a directory named `%TEMP%\CefFlashBrowser-regression-<guid>` for CEF cache, HTML fixtures, `browser.db`, `app.log`, and `cef.log`. Settings and favorites are supplied in memory instead of loading the user's profile; any data writes are redirected to this directory. The favicon fixture listens only on loopback with an automatically assigned port. The directory is retained, and its path is printed as soon as it is created so diagnostics remain available even if CEF stops responding. Process and missing-build checks can fail before any directory is created.

When adding checks, put the scenario in the relevant scenario file and register any new top-level scenario explicitly in `Run.ps1`. Put reusable browser operations in `Helpers.ps1`, and keep resource ownership and initialization in `Environment.ps1`.
