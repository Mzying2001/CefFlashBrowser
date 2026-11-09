# Command proxy regression checks

Build the solution with VS MSBuild, then run in Windows PowerShell 5.1:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\CommandProxyRegression\Run.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File CefFlashBrowser.Tests\CommandProxyRegression\Run.ps1 -Platform x86 -Configuration Release
```

The default proxy path is `Binaries\CmdProxy\CmdProxy.exe` under the selected build output. `-Platform` selects the executable architecture, and `-Configuration` defaults to Debug. `-ProxyPath` can select an executable extracted from a release archive. The package smoke check also runs these checks against the packaged proxy.

The runner compiles a temporary .NET Framework test executable and copies the native proxy into a directory with spaces and Chinese characters. It compares the real proxy with system cmd for raw quoting, pipes, file redirection, binary stream preservation, standard input, environment and working-directory inheritance, and full process exit codes. Actual CRT `system()` and `_wsystem()` calls check the COMSPEC integration. A separate hidden console checks console reuse and Ctrl+C/Ctrl+Break without sending control events to the caller's console. All processes have bounded waits. Temporary files are retained at the printed path for diagnostics.

The proxy filters only `/c` (or `/C`) followed by the complete `echo NOT SANDBOXED` command, optionally surrounded by one pair of double quotes. Surrounding spaces and tabs are accepted. Additional switches, other letter casing in the command text, redirection, pipes and appended commands are forwarded unchanged. The forwarded cmd receives the inherited environment with COMSPEC restored to its own system path.
