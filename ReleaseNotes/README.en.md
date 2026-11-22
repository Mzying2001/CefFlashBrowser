# GitHub Release Notes

[中文](./README.md) | **English**

This CLI generates bilingual Markdown from the changes between the tag of the most recent stable GitHub Release and local `HEAD`. It requires Python 3.10+, Git, and Bash. On Windows, you can use the Bash included with Git for Windows.

Set the `RELEASE_NOTES_API_KEY` environment variable and run this command from the repository root.

```powershell
python ReleaseNotes/generate.py
```

To use `--api-key-file`, pass the path to an existing, Git-ignored file containing one line with a valid API key. The CLI reports an error if the file is missing or empty.

On success, standard output contains UTF-8 Markdown ready to paste into a GitHub Release. The CLI examines committed changes and uses the bodies of the three most recent stable releases as style references. The configured model service receives those references and any commit messages, diffs, and source code it requests through the tools. Review the generated body before pasting it into the release page.

| Setting | Default |
| --- | --- |
| `--repo` | Find the repository root from the current directory |
| `--github-repo OWNER/REPO` | Use the GitHub repository configured as `origin` |
| `--base-tag` | Tag of the most recent stable GitHub Release |
| `--api-base-url` or `RELEASE_NOTES_API_BASE_URL` | `https://api.deepseek.com` |
| `--model` or `RELEASE_NOTES_MODEL` | `deepseek-flash` |
| `--api-key-file` or `RELEASE_NOTES_API_KEY` | Read the specified key file if provided, otherwise use the environment variable |

Supply the API base URL. The CLI appends `/chat/completions`, and the service must support Chat Completions tool calls. Command-line options take precedence over environment variables. The CLI uses the model credential only in the authorization header. Store it in a CI secret or a Git-ignored local file.

In CI, check out the target commit and fetch the full history, including the base tag, before running the same command. In Bash, use `python ReleaseNotes/generate.py > release-notes.md` to save the result. On failure, the CLI reports the reason to standard error and returns a nonzero exit status.

## Project layout

- `generate.py` is the CLI entry point.
- `src/release_notes/` contains the generation workflow and read-only Git tools.
- `tests/` contains regression tests.

## Tests

```powershell
python -m unittest discover -s ReleaseNotes/tests -v
```

The tests use temporary Git repositories and a mock API. They do not require a real API key.
