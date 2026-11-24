# Contributing

[中文](./CONTRIBUTING.md) | **English**

Bug reports, feature requests, translations, documentation, and code changes are welcome. You can open issues and pull requests in Chinese or English.

## Open an issue

[Search existing issues](https://github.com/Mzying2001/CefFlashBrowser/issues?q=is%3Aissue) first. If someone has already reported the same problem, add your reproduction details to that issue.

- **Bug reports.** If possible, confirm the problem on the [latest release](https://github.com/Mzying2001/CefFlashBrowser/releases/latest). Include the app version, Windows version, app architecture, steps to reproduce, and expected and actual behavior. For Flash playback, include a page URL or sample SWF if available. For save issues, name the operation that failed and include a sample SOL if available. App and CEF logs are in the `Logs` folder next to `CefFlashBrowser.exe`.
- **Feature requests.** Describe the use case, what gets in the way today, and what you would like to happen. For substantial changes, consider opening an issue to discuss the idea first.

[Choose an issue template](https://github.com/Mzying2001/CefFlashBrowser/issues/new/choose). Remove credentials and personal information from logs, screenshots, and samples before sharing them publicly.

## Build and test

On Windows, use Visual Studio 2022 with `.slnx` support and a .NET SDK. Visual Studio 2022 17.14 with .NET SDK 9.0 is a tested combination. In Visual Studio Installer, select the **.NET desktop development** and **Desktop development with C++** workloads. Make sure the **.NET Framework 4.6.2 targeting pack**, **Windows SDK**, and **C++/CLI support for v143 build tools** are installed.

Open the **x64 Native Tools Command Prompt for VS 2022** and navigate to the repository root. The solution includes C++/CLI projects, so build it with Visual Studio `msbuild`.

```cmd
set "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=%ProgramFiles%\dotnet"
msbuild CefFlashBrowser.slnx -restore -p:Configuration=Debug -p:Platform=x64
```

After building, run the app from `bin\x64\Debug\CefFlashBrowser.exe`. If your .NET SDK is installed elsewhere, set `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` to that installation directory.

Run the unit tests using the command below. To build just the test project, substitute `CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj` for the solution file in the MSBuild command above.

```cmd
dotnet test CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build --no-restore
```

For changes to WPF windows, the browser, or CEF integration, build the full solution and run the relevant [browser regression checks](./CefFlashBrowser.Tests/BrowserRegression/README.md) in an interactive Windows desktop session.

## Open a pull request

1. Fork the repository, create a working branch from `dev`, and target the upstream `dev` branch in your pull request.
2. Keep the change focused on one issue or feature and follow [`.editorconfig`](./.editorconfig). For UI text changes, check the corresponding localization resources in [`CefFlashBrowser/Assets/Language`](./CefFlashBrowser/Assets/Language).
3. Preserve the existing project configuration, including the .NET target framework version, dependency versions, and build platforms.
4. Use Conventional Commits with a concise English summary, for example `fix(sol): preserve empty arrays`. For substantial changes, explain why and how in the commit body.
5. In the pull request, explain the change and its impact, reference related issues by number (for example, `#123`), and report your test commands, manual checks, and results. Include screenshots or a recording if they help show a UI change.
