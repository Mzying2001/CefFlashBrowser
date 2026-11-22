# GitHub Release 更新日志

**中文** | [English](./README.en.md)

这个 CLI 根据 GitHub 最近一次正式 Release 的 tag 和本地 `HEAD` 之间的变更，生成中英双语 Markdown。需要 Python 3.10+、Git 与 Bash。Windows 上可使用 Git for Windows 自带的 Bash。

在仓库根目录运行以下命令。将 API 密钥注入 `RELEASE_NOTES_API_KEY` 环境变量后，可直接运行，无需提供其他参数。

```powershell
python ReleaseNotes/generate.py
```

若使用 `--api-key-file`，请传入一个已存在、被 Git 忽略的文件路径，并在文件中写入一行有效密钥。文件不存在或为空时，CLI 会报错。

成功时，标准输出是可粘贴到 GitHub Release 的 UTF-8 Markdown。CLI 查看已提交的内容，用 GitHub 最近三次正式发布的正文作为风格参考。配置的模型服务会收到这些参考日志，以及模型通过工具读取的提交说明、差异和源码。将生成的正文审核后粘贴到发布页面。

| 配置 | 默认值 |
| --- | --- |
| `--repo` | 从当前目录定位仓库根目录 |
| `--github-repo OWNER/REPO` | 使用 `origin` 指向的 GitHub 仓库 |
| `--base-tag` | GitHub 最近一次正式发布的 tag |
| `--api-base-url` 或 `RELEASE_NOTES_API_BASE_URL` | `https://api.deepseek.com` |
| `--model` 或 `RELEASE_NOTES_MODEL` | `deepseek-flash` |
| `--api-key-file` 或 `RELEASE_NOTES_API_KEY` | 使用显式传入的密钥文件，否则读取环境变量 |
| `--max-tool-rounds` | 最多 32 轮工具查询 |
| `--max-tool-calls` | 总共最多 64 次工具调用 |

每轮可以包含多次工具调用。达到任一上限时，CLI 会用已收集的信息请求最终正文。可通过这两个参数按发布区间调整预算。

API 地址填基址。CLI 会追加 `/chat/completions`，服务需支持 Chat Completions 工具调用。命令行参数优先于环境变量。模型凭据只用于认证请求头。密钥应放在 CI secret 或 Git 忽略的本地文件中。

在 CI 中，检出目标提交和包含基线 tag 的完整历史，再运行同一个命令。在 Bash 中可通过 `python ReleaseNotes/generate.py > release-notes.md` 收集正文。失败时 CLI 在标准错误报告原因并返回非零状态。

## 项目结构

- `generate.py` 提供 CLI 入口。
- `src/release_notes/` 包含日志生成流程和只读 Git 工具。
- `tests/` 包含回归测试。

## 测试

```powershell
python -m unittest discover -s ReleaseNotes/tests -v
```

测试使用临时 Git 仓库和模拟 API，无需真实密钥。
