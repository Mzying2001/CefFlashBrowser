# 贡献指南

**中文** | [English](./CONTRIBUTING.en.md)

欢迎提交问题反馈、功能建议、翻译、文档和代码改动。Issue 和 Pull Request 均可使用中文或英文。

## 提交 Issue

先[搜索已有 Issue](https://github.com/Mzying2001/CefFlashBrowser/issues?q=is%3Aissue)。如果已有相同的问题，可以在原帖补充复现信息。

- **问题反馈**。尽可能用[最新发布版本](https://github.com/Mzying2001/CefFlashBrowser/releases/latest)确认问题。填写软件版本、Windows 版本、程序架构、复现步骤及预期和实际结果。Flash 播放问题可附网页 URL 或 SWF 样例，存档问题可注明失败的操作并附上 SOL 样例。应用和 CEF 的日志在 `CefFlashBrowser.exe` 同目录下的 `Logs` 文件夹。
- **功能建议**。描述实际使用场景、目前遇到的困难以及期望的行为。涉及较大的功能改动时，建议先开 Issue 讨论。

[选择 Issue 模板](https://github.com/Mzying2001/CefFlashBrowser/issues/new/choose)。公开分享日志、截图或样例前，请移除账号凭据和个人信息。

## 构建与测试

在 Windows 上使用支持 `.slnx` 的 Visual Studio 2022 和 .NET SDK。已验证的组合为 Visual Studio 2022 17.14 和 .NET SDK 9.0。通过 Visual Studio Installer 安装 **.NET 桌面开发**和**使用 C++ 的桌面开发**工作负载，并确认安装了 **.NET Framework 4.6.2 目标包**、**Windows SDK** 和 **对 v143 生成工具的 C++/CLI 支持**。

从 **x64 Native Tools Command Prompt for VS 2022** 进入仓库根目录，使用 Visual Studio 的 `msbuild` 构建。解决方案包含 C++/CLI 项目，需要此工具链。

```cmd
set "DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=%ProgramFiles%\dotnet"
msbuild CefFlashBrowser.slnx -restore -p:Configuration=Debug -p:Platform=x64
```

构建成功后，可从 `bin\x64\Debug\CefFlashBrowser.exe` 启动应用。若 .NET SDK 安装在其他目录，请将 `DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR` 指向实际安装目录。

使用下方命令运行单元测试。若只需构建测试项目，可在上面的 MSBuild 命令中用 `CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj` 替换解决方案文件。

```cmd
dotnet test CefFlashBrowser.Tests\CefFlashBrowser.Tests.csproj -p:Platform=x64 --no-build --no-restore
```

改动涉及 WPF 窗口、浏览器或 CEF 集成时，先构建完整解决方案，再在交互式 Windows 桌面上运行相关的[浏览器回归检查](./CefFlashBrowser.Tests/BrowserRegression/README.md)。

## 提交 Pull Request

1. Fork 仓库，从 `dev` 创建工作分支，并将 PR 的目标分支设为上游的 `dev`。
2. 将改动集中在一个问题或功能上，并遵循 [`.editorconfig`](./.editorconfig)。修改界面文字时，请检查 [`CefFlashBrowser/Assets/Language`](./CefFlashBrowser/Assets/Language) 中对应的本地化资源。
3. 保留现有项目配置，包括 .NET 目标框架版本、依赖版本和构建平台。
4. 提交消息使用 Conventional Commits 格式，标题用简洁的英文，例如 `fix(sol): preserve empty arrays`。较复杂的改动请在提交正文中说明原因和实现方式。
5. 在 PR 中说明改动目的和影响，用 `#123` 等编号关联 Issue，并记录测试命令、手动验证步骤及结果。界面改动可附截图或录屏。
