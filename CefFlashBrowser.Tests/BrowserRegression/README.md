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

Download checks can be run independently with `Run.ps1 -Scenario DownloadManager`, `-Scenario BrowserDownloads`, or `-Scenario Downloads` for the userscript contract. They cover the shared window and destination dialog, native keyboard entry, actual file bytes, pause/resume, POST and Blob sources, background native ownership, and file actions. The default suite includes these scenarios.

Run `Run.ps1 -Scenario DownloadRestart` separately for cross-process recovery. It starts an isolated HTTP fixture and two hidden STA PowerShell processes against the same temporary download profile and CEF cache. The first process saves unfinished browser and manual tasks before shutdown. The second restores them, verifies the persisted Range offsets and cookies, and compares the final files with the fixture payload. All profiles, files and process logs remain under the printed temporary directory.

Run `Run.ps1 -Scenario Polyfills` to exercise the first script on each probe document with userscripts disabled. The check covers strict CSP, same-origin and cross-origin iframes, `srcdoc`, `about:blank`, `blob:`, `data:`, local HTML, reload, and a real popup. The package smoke check also runs the first-script probe using the renderer from the supplied zip.

Run `Run.ps1 -Scenario PolyfillsDisabled` in a fresh STA process to verify that `"EnableCoreJsPolyfills": false` in `settings.json` disables injection through the application startup path.

After `publish`, validate each actual zip in a fresh process of the matching architecture:

```cmd
powershell -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\PackageSmoke.ps1 -ArchivePath bin\Publish\FlashBrowser_x64_v1.1.8.zip -Platform x64
"%SystemRoot%\SysWOW64\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\BrowserRegression\PackageSmoke.ps1 -ArchivePath bin\Publish\FlashBrowser_x86_v1.1.8.zip -Platform x86
```

Adjust the version in the archive names when needed. The smoke check extracts only the supplied archive into an isolated temporary directory and initializes the executable's Costura resolver for embedded managed assemblies. It verifies that runtime dependencies are deployed under `Binaries\CefSharp`, `Binaries\CmdProxy`, `Binaries\SQLite`, `Binaries\Plugins`, and `Binaries\VCRuntime`, with no external `Assets` directory or dependency archives. It checks the VC++ DLL architecture, verifies that the application preloads them from `Binaries\VCRuntime`, and verifies that the CEF subprocess loads identical copies from `Binaries\CefSharp`. It requires exactly one SQLite native library at `Binaries\SQLite\SQLite.Interop.dll` and verifies that the process loads that file using the application's native DLL search-directory initialization. It exercises the real IoC factories, SQLite persistence and shutdown, and checks that unregistering unused services neither creates a database nor loads SQLite. It also runs the [command proxy regression checks](../CommandProxyRegression/README.md) against the packaged native executable. It then starts the packaged CEF in a temporary profile, attaches a host before native creation, prepares the isolated world and navigates to a local fixture. The fixture verifies synchronous restoration of stored undefined and a committed asynchronous GM write through the real bridge. Native CEF must load from that extracted archive. No project-output or NuGet fallback is used, and no external network resources are requested.

The manager workflow can also run alone with `-Scenario Manager`. It opens the actual single-instance window, exercises source editing, direct new-script saving, list toggles and real CEF execution, then tests local/URL installation confirmation, draft retention, export and language/theme changes. Light/dark captures are written to `%TEMP%/cfb-manager-{Light,Dark}-{x64,x86}.png`.

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
| `Scenarios/BrowserTabs.ps1` | Tab strip buttons, middle click, keyboard cycling, drag reordering and edge scrolling, preview content for later and scrolled tabs, overflow, titles and fullscreen; localized tab menus with reload/stop and mute grouped first; foreground/background tab reload and stop, live loading-state updates and shared-menu retargeting; actions-menu mute after new window, shared-menu retargeting and background-tab isolation; native Ctrl+M from page and address-bar focus with shortcut hints; mute state checked against native CEF audio, repeated toggles, nonempty tab-icon drawings and rendered colors across dark/light theme switches; duplication and new-window actions; sequential close-other-tabs cancellation, retry and interruption; foreground/background new-page routing; retained browser/DevTools state; source-specific callbacks; close cancellation and retry; sequential window close; and forced disposal of initializing tabs. |
| `Scenarios/ContextMenu.ps1` | Search and selected-URL routing for every new-page setting, explicit new-tab/new-window commands, page-menu composition without mute actions in either audio state, translations, closed-browser callbacks, and auxiliary-window routing. |
| `Scenarios/ViewSource.ps1` | Source viewing from the actions menu, Ctrl+U and page context menu for every new-page setting; native HTML and window fullscreen, tab ordering, source-address preservation, reload, duplication, history, DevTools, ordinary closing, late callbacks, and auxiliary-window routing. |
| `Scenarios/AuxiliaryWindows.ps1` | Popup and SWF player windows: bindings, routing, DevTools, beforeunload, and cleanup; embedded SWF player reloads, local and network source assignment, and independent player windows without external HTML files. |
| `Scenarios/UserScripts.ps1` | Persistent fixture installation, navigation and disposal, real menus and tab handles, clipboard restoration, notification interactions, committed downloads and native mute synchronization. |
| `Scenarios/UserScriptNetwork.ps1` | Loopback network/cookie and static-rule evidence, main/same-origin/cross-port frames, cross-window storage events, offline asset loading, external source reload, cache rebuilding and local failure isolation. |
| `UserScriptHttpFixture.cs`, `UserScriptFrameProbe.cs`, `UserScriptRuntimeProbe.cs` | Isolated HTTP evidence and native-frame evaluation; restart the shared runtime against the same temporary database for offline/recovery checks. |
| `PackageBrowserSmoke.ps1` | Real CEF injection and storage through assemblies resolved only from the supplied release zip. |
| `Scenarios/Lifecycle.ps1` | Operations before CEF initialization, navigation before native hosting, closing before Show or during initialization, and the close-all message. |

Only `Run.ps1` executes the suite. The supporting PowerShell files define functions without starting WPF or CEF when loaded. Functions receive a run context explicitly when they need shared resources; individual windows and browsers remain local to their scenarios.

The order is intentional: prepare WPF; verify language initialization and switching and favorites persistence; check deferred browsers, the standalone tab control and tab management before CEF initialization; start CEF; run userscript runtime, host-interaction, network, frame and recovery fixtures; check navigation before native hosting; close auxiliary windows before Show; exercise the main window, favorites surfaces and favicons; restore saved state; close initializing main windows; exercise multiple tabs and context menus; exercise auxiliary windows; send close-all to windows with ready and initializing tabs; clean up. CEF starts once, and the checks run serially. Settings such as zoom and DevTools width carry across checks that verify persistence, so keep those checks together and in order.

## Test data and diagnostics

Each run creates a directory named `%TEMP%\CefFlashBrowser-regression-<guid>` for CEF cache, HTML fixtures, `browser.db`, `app.log`, and `cef.log`. Settings and favorites are supplied in memory instead of loading the user's profile; any data writes are redirected to this directory. The favicon fixture listens only on loopback with an automatically assigned port. The directory is retained, and its path is printed as soon as it is created so diagnostics remain available even if CEF stops responding. Process and missing-build checks can fail before any directory is created.

When adding checks, put the scenario in the relevant scenario file and register any new top-level scenario explicitly in `Run.ps1`. Put reusable browser operations in `Helpers.ps1`, and keep resource ownership and initialization in `Environment.ps1`.

## Userscript dependency baseline

Run `Dependencies.ps1` in a fresh architecture-matched STA Windows PowerShell process after building.
It validates MonkeySharp 5.0.0, the CefSharp 87.1.132 reference, typed JSON, embedded bridge assets,
and AvalonEdit with its embedded JavaScript definition (loaded through Costura in Release)
without starting CEF. Pass `-Platform x86` or `-Configuration Release` as appropriate;
`-ApplicationPath` also supports an extracted release. Dependencies must resolve from that output
or its Costura resources, never from the developer NuGet cache.

The initial Debug x64 baseline on 2026-11-15 built successfully and passed all 295 unit tests.
The full browser runner stopped at `status popup avoids the cursor using the vertical interval`
before userscript changes; this remains a separately recorded baseline failure, not a passing suite.

Use `Run.ps1 -Scenario UserScriptSeams` for the focused real-CEF creation, preparation, worker-readiness, async binding and native audio checks. The default runs the full suite. The initial status-popup failure did not recur in the subsequent full Debug x64 run.

The runtime scenarios can be selected with `-Scenario UserScripts`, `BrowserDisposal`, `Loading`,
`TabRouting`, `ViewSource`, or `Lifecycle`. They use the same isolated environment as the full suite.
`UserScripts` installs temporary persistent fixtures through the repository and verifies first-page
DOM execution, isolated synchronous storage bootstrap, SQLite writes, reload and stable tab identity.
The disposal checks also cover direct disposal from a frame callback. Loading checks assert that
stopping a CDP navigation never restarts its request. Background browser views remain hidden while
participating in native hosting; SWF's application-owned page and source-view pages exclude scripts.

`-Scenario Closing` verifies real isolated GM calls after rejected beforeunload in browser,
popup and SWF windows, followed by accepted-close cleanup. `-Scenario Auxiliary` runs the existing
auxiliary routing, embedded player, DevTools, confirmation and force-close checks. Window helpers
wait for the actual target document rather than the runtime's temporary blank document.

`-Scenario Menus` verifies five real manual scripts alongside application commands, dynamic GM
callbacks, native frame attestation, the manual-menu cap, global menu suppression and registration
cleanup. `-Scenario ScriptTabs` covers actual GM tab handles, rejected/accepted closes, independent
auxiliary sources, tab-state navigation and cleanup, unsupported privacy options and concurrent
last-browser protection. `-Scenario ContextMenu` retains the existing application menu regression.
The pinned library keeps JavaScript window actions void and dispatches a handle's close once;
rejected closes remain open and later user closes still produce the handle's actual close notification.

`-Scenario Interactions` writes known Unicode text and CF_HTML through real GM calls and checks
UTF-8 fragment offsets. The original clipboard is privately snapshotted in memory and restored in
`finally` after the source stops. It exercises unstarted notification handles, real WPF buttons,
click acknowledgement/preventDefault, source-bound URL actions, tag replacement, native close,
timeout, bounded image loading and cancellation of an in-flight image request during shutdown.

`-Scenario Downloads` verifies unstarted operations, CEF response bytes, one terminal event,
real permission choices, filename uniquification, Blob staging, cancellation of the native save
picker, history cancellation, failed/aborted/timed-out cleanup, document navigation, replacement
of a host by source viewing, and tab-close draining. Downloads go only to the isolated profile's
Downloads directory. It also verifies bidirectional GM/native/UI mute. `-Scenario Audio` retains
the original menu, icon, keyboard, background-tab and rapid-toggle mute regression.

The browser session owns the actual CEF request context, CEF HTTP/rule services and mute service
across host replacements. A detached host with accepted downloads retires until those transfers
finish; its callbacks are revoked immediately. Closing cancels and drains active/retired downloads
before disposing the HTTP context and native browser. This keeps GM traffic on CEF, including its
actual cookie jar, while installation content remains on the separate credential-free client.


`-Scenario Network` checks first-request static rules, including a blocked execution world that
must leave ordinary resources untouched. The host uses a filtered view of the shared repository,
so unavailable installations and unsupported execution worlds register no static rules. Installations
remain in the shared repository with their readiness and diagnostics. The fixture also verifies the actual CEF login jar,
anonymous requests, redirect permissions, undeclared destinations, native cookie CRUD and
rejection of unsupported partition/explicit-Cookie options before server-side effects.

`-Scenario Frames` verifies first-statement bootstrap in main, same-origin and cross-port frames,
source execution once, noframes, SPA URL notifications without reinjection, and actual local/remote
storage events across frames, a background tab and a second window. `-Scenario Recovery` restarts
the runtime against the same temporary database and separate source/cache roots. It loads assets
offline without HTTP, adopts external source edits without reinjecting the current document,
rebuilds missing cached dependencies, preserves identity/timestamps/GM values, and keeps a healthy
neighbor executing when another installation is unavailable. Static rules are absent during cache
loss and revoked after an invalid-source reload. Cache removal follows service shutdown.

`-Scenario TabClosing` isolates native Ctrl+W cancellation/retry, removing an opener while its
child awaits confirmation, and reload/close confirmation ordering. Helpers wait for the actual
target document before installing beforeunload; the runtime's warm-up blank can already execute
JavaScript while its public Address still names a pending navigation.


For Release, runtime-compiled probe references are extracted from the built executable's
compressed Costura resources into the temporary test directory. Release resolution does not
fall back to project outputs or the NuGet cache. The separate package smoke leaves those
resources embedded and verifies Costura resolution while running the real browser.


## Stage A acceptance (2026-11-15)

| Configuration | VS MSBuild | Unit tests | Full browser regression |
| --- | --- | --- | --- |
| Debug x64 | Passed | 357 passed | Passed |
| Debug x86 | Passed | 357 passed | Passed |
| Release x64 | Passed | 357 passed | Passed |
| Release x86 | Passed | 357 passed | Passed |

`publish.ps1` created both version 1.1.8 archives. Dependency checks passed for both architectures
in Debug and Release. Both actual archives passed native deployment/command-proxy checks,
lazy-service and SQLite persistence checks, real isolated-world injection and GM read/write,
and orderly CEF/storage shutdown. The existing Costura IncludeAssets warning remains.

## Persistence v4 acceptance (2026-11-17)

| Configuration | VS MSBuild | Unit tests | Real CEF | Dependency check | Archive smoke |
| --- | --- | --- | --- | --- | --- |
| Debug x64 | Passed | 395 passed | Full suite and Recovery passed | Passed | Not applicable |
| Debug x86 | Passed | 395 passed | Recovery passed | Passed | Not applicable |
| Release x64 | Passed | 395 passed | Recovery passed | Passed | Passed |
| Release x86 | Passed | 395 passed | Recovery passed | Passed | Passed |

The two version 1.1.8 archives were produced by `publish.ps1`. Each archive smoke installs assets
from a loopback fixture, checks schema 2 and separate content-addressed files, then restores
source and assets offline through the public persistence contract. It also verifies real CEF
injection, synchronous typed-value bootstrap, acknowledged GM writes, native SQLite deployment,
Costura loading and orderly shutdown using only the extracted archive. The existing Costura
IncludeAssets warning remains.
