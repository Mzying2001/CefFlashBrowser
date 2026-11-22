from contextlib import contextmanager, redirect_stderr, redirect_stdout
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import io
import json
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory
from threading import Thread
import unittest
from unittest.mock import patch

from ReleaseNotes.src.release_notes import generate
from ReleaseNotes.src.release_notes.repository import Git, HarnessError, RepositoryRange


MOCK_KEY = "mock-release-notes-credential"
SOURCE = "Feature [预览].txt"
RELEASE_BODY = "### 中文\n\n- 新增预览\n\n### English\n\n- Added preview"


def completion(*calls, content=None):
    message = {"role": "assistant", "content": content}
    if calls:
        message["tool_calls"] = list(calls)
    return {"choices": [{"message": message, "finish_reason": "tool_calls" if calls else "stop"}]}


def tool_call(call_id, name, action, **arguments):
    return {
        "id": call_id,
        "type": "function",
        "function": {"name": name, "arguments": json.dumps({"action": action, **arguments})},
    }


@contextmanager
def mock_chat(*responses):
    requests = []

    class Handler(BaseHTTPRequestHandler):
        def do_POST(self):
            data = self.rfile.read(int(self.headers["Content-Length"]))
            requests.append((self.path, self.headers.get("Authorization"), json.loads(data)))
            status, headers, body = responses[len(requests) - 1]
            self.send_response(status)
            for name, value in headers.items():
                self.send_header(name, value)
            self.end_headers()
            self.wfile.write(json.dumps(body).encode("utf-8"))

        def log_message(self, *args):
            pass

    with ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
        thread = Thread(target=server.serve_forever, daemon=True)
        thread.start()
        try:
            yield f"http://127.0.0.1:{server.server_port}/v1", requests
        finally:
            server.shutdown()
            thread.join()


def release(tag, published_at, **fields):
    return {"tag_name": tag, "published_at": published_at, "name": tag, "body": f"Body for {tag}", **fields}


class HarnessTests(unittest.TestCase):
    def setUp(self):
        temporary = TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name) / "sample repo"
        self.root.mkdir()
        self.native_git("init", "-b", "main")
        self.native_git("config", "user.email", "tests@example.invalid")
        self.native_git("config", "user.name", "Release Notes Test")
        self.native_git("config", "core.autocrlf", "false")
        self.native_git("remote", "add", "origin", "git@github.com:example/preview.git")
        (self.root / SOURCE).write_text("旧版本\n", encoding="utf-8", newline="\n")
        self.native_git("add", ".")
        self.native_git("commit", "-m", "Initial version")
        self.native_git("tag", "-a", "1.0", "-m", "First release")
        (self.root / SOURCE).write_text("新增预览\n$(touch PWNED)\n", encoding="utf-8")
        private = self.root / "local-notes"
        private.mkdir()
        (private / "excluded.txt").write_text("excluded local data\n", encoding="utf-8")
        self.native_git("add", ".")
        self.native_git("commit", "-m", "feat: add preview", "-m", "Preview changes before saving.")
        (self.root / SOURCE).write_text("uncommitted change\n", encoding="utf-8")
        self.native_git("tag", "unpublished-2.0")
        self.head = self.native_git("rev-parse", "HEAD").strip()
        self.git = Git(self.root)
        self.repository = RepositoryRange(self.git, "1.0", self.head)

    def native_git(self, *args):
        return subprocess.run(
            ["git", *args], cwd=self.root, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True,
        ).stdout.decode("utf-8")

    def test_cli_release_references_and_tool_round_trip(self):
        references = [
            release("0.8", "2026-09-01T00:00:00Z"),
            release("1.0", "2026-11-01T00:00:00Z"),
            release("0.9", "2026-10-01T00:00:00Z"),
            release("rc", "2026-11-20T00:00:00Z", prerelease=True),
            release("draft", "2026-11-21T00:00:00Z", draft=True),
        ]
        calls = [
            tool_call("history", "git_history", "list"),
            tool_call("files", "git_changes", "files"),
            tool_call("search", "git_source", "search", query="新增预览"),
        ]
        details = [
            tool_call("commit", "git_history", "commit", commit=self.head[:12]),
            tool_call("diff", "git_changes", "diff", path=SOURCE),
            tool_call("read", "git_source", "read", path=SOURCE, revision="base"),
        ]
        model_response = completion(*calls)
        model_response["choices"][0]["message"]["reasoning_content"] = "Check final repository behavior."
        real_request = generate.request_json

        def request(url, service, **kwargs):
            if service == "GitHub":
                self.assertEqual(url, "https://api.github.com/repos/example/preview/releases?per_page=100&page=1")
                self.assertEqual(kwargs, {})
                return references
            return real_request(url, service, **kwargs)

        out, err = io.StringIO(), io.StringIO()
        key_file = self.root.parent / "test-key.txt"
        key_file.write_text(MOCK_KEY + "\n", encoding="utf-8-sig")
        env = {"RELEASE_NOTES_API_KEY": "wrong-key", "RELEASE_NOTES_API_BASE_URL": "https://example.invalid"}
        with mock_chat(
            (200, {}, model_response),
            (200, {}, completion(*details)),
            (200, {}, completion(content=RELEASE_BODY)),
        ) as (url, requests), patch.object(generate, "request_json", side_effect=request), patch.dict(
            "os.environ", env,
        ), redirect_stdout(out), redirect_stderr(err):
            code = generate.main([
                "--repo", str(self.root), "--api-base-url", url, "--api-key-file", str(key_file), "--model", "test-model",
                "--max-tool-rounds", "2", "--max-tool-calls", "6",
            ])

        self.assertEqual((code, out.getvalue(), err.getvalue()), (0, RELEASE_BODY + "\n", ""))
        self.assertEqual(len(requests), 3)
        self.assertEqual(requests[-1][2]["tool_choice"], "none")
        for path, authorization, body in requests:
            self.assertEqual(path, "/v1/chat/completions")
            self.assertEqual(authorization, f"Bearer {MOCK_KEY}")
            self.assertNotIn(MOCK_KEY, json.dumps(body))
        first = requests[0][2]
        self.assertEqual(first["model"], "test-model")
        prompt = Path(generate.__file__).with_name("system_prompt.md").read_text(encoding="utf-8")
        self.assertEqual(first["messages"][0], {"role": "system", "content": prompt})
        context = json.loads(first["messages"][1]["content"].split("\n", 1)[1])
        self.assertEqual(context["base_tag"], "1.0")
        self.assertEqual(context["head_commit"], self.head)
        self.assertIn("feat: add preview", context["commit_overview"]["text"])
        self.assertIn(SOURCE, context["changed_files"]["text"])
        self.assertEqual(
            [(item["tag"], item["body"]) for item in context["prior_releases"]],
            [(tag, f"Body for {tag}") for tag in ("1.0", "0.9", "0.8")],
        )
        messages = requests[-1][2]["messages"]
        self.assertEqual(messages[2]["reasoning_content"], "Check final repository behavior.")
        results = {
            message["tool_call_id"]: json.loads(message["content"])["text"]
            for message in messages if message["role"] == "tool"
        }
        self.assertIn(self.head, results["history"])
        self.assertIn(SOURCE, results["files"])
        self.assertNotIn("local-notes", results["files"])
        self.assertIn("新增预览", results["search"])
        self.assertIn("Preview changes before saving.", results["commit"])
        self.assertIn("+新增预览", results["diff"])
        self.assertEqual(results["read"], "旧版本\n")

    def test_cli_prepends_download_badge_for_new_release_tag(self):
        for tag, badge_tag in (("2.0.0", "2.0.0"), ("preview/2.0.0", "preview%2F2.0.0")):
            with self.subTest(tag=tag):
                out = io.StringIO()
                github = "Mzying2001/CefFlashBrowser"
                with patch.dict("os.environ", {"RELEASE_NOTES_API_KEY": MOCK_KEY}), patch.object(
                    generate, "fetch_releases", return_value=[{"tag": "1.0"}],
                ), patch.object(generate, "generate_notes", return_value=RELEASE_BODY) as notes, redirect_stdout(out):
                    code = generate.main(["--repo", str(self.root), "--github-repo", github, "--release-tag", tag])

                badge = f"![](https://img.shields.io/github/downloads/{github}/{badge_tag}/total)"
                self.assertEqual((code, out.getvalue()), (0, f"{badge}\n\n{RELEASE_BODY}\n"))
                self.assertEqual(notes.call_args.args[0].tag, "1.0")

    def test_cli_rejects_invalid_or_sensitive_release_tags(self):
        for tag in ("", "bad tag", MOCK_KEY):
            with self.subTest(tag=tag):
                out, err = io.StringIO(), io.StringIO()
                with patch.dict("os.environ", {"RELEASE_NOTES_API_KEY": MOCK_KEY}), patch.object(
                    generate, "fetch_releases",
                ) as releases, redirect_stdout(out), redirect_stderr(err):
                    code = generate.main(["--repo", str(self.root), "--release-tag", tag])

                self.assertEqual((code, out.getvalue()), (1, ""))
                self.assertNotIn(MOCK_KEY, err.getvalue())
                releases.assert_not_called()

    def test_git_rejects_unsafe_arguments_and_only_reads_commit_objects(self):
        for path in ("../outside", "/outside", "C:/outside", ".git/config", "Local-Notes/excluded.txt"):
            with self.subTest(path=path):
                with self.assertRaises(HarnessError):
                    self.repository.source("read", {"path": path})
        self.assertNotIn("uncommitted change", self.repository.source("read", {"path": SOURCE}))
        self.assertEqual(self.repository.source("search", {"query": "excluded local data"}), "")
        self.assertIn("$(touch PWNED)", self.repository.source("search", {"query": "$(touch PWNED)"}))
        self.assertFalse((self.root / "PWNED").exists())
        base = self.native_git("rev-parse", "1.0^{commit}").strip()
        with self.assertRaisesRegex(HarnessError, "release range"):
            self.repository.history("commit", {"commit": base})
        with patch.dict("os.environ", {"RELEASE_NOTES_API_KEY": MOCK_KEY, "BASH_ENV": "untrusted.sh"}):
            env = Git(self.root).env
        self.assertNotIn("RELEASE_NOTES_API_KEY", env)
        self.assertNotIn("BASH_ENV", env)

    def test_invalid_base_tags(self):
        with self.assertRaisesRegex(HarnessError, "Cannot resolve local base tag"):
            RepositoryRange(self.git, "missing", self.head)
        with self.assertRaisesRegex(HarnessError, "same commit"):
            RepositoryRange(self.git, "unpublished-2.0", self.head)
        unrelated = self.native_git("commit-tree", "HEAD^{tree}", "-m", "Unrelated commit").strip()
        self.native_git("tag", "unrelated", unrelated)
        with self.assertRaisesRegex(HarnessError, "not an ancestor"):
            RepositoryRange(self.git, "unrelated", self.head)

    def test_tool_pagination_and_redaction(self):
        text = "句" * (generate.PAGE_SIZE - 2) + MOCK_KEY + "\nLast line"
        with patch.object(self.repository, "history", return_value=text):
            first = generate.run_tool(self.repository, tool_call("a", "git_history", "list"), MOCK_KEY)
            second = generate.run_tool(
                self.repository, tool_call("b", "git_history", "list", offset=first["next_offset"]), MOCK_KEY,
            )
        self.assertLessEqual(len(first["text"]), generate.PAGE_SIZE)
        self.assertEqual(first["text"] + second["text"], text.replace(MOCK_KEY, "[REDACTED]"))
        self.assertIsNone(second["next_offset"])

    def test_api_errors_and_redirects_omit_response_bodies(self):
        for status, headers in [(401, {}), (307, {"Location": "/redirect"})]:
            with self.subTest(status=status), mock_chat((status, headers, {"message": MOCK_KEY})) as (url, requests):
                with self.assertRaisesRegex(HarnessError, f"returned HTTP {status}") as error:
                    generate.generate_notes(self.repository, [], url, "test-model", MOCK_KEY)
                self.assertNotIn(MOCK_KEY, str(error.exception))
                self.assertEqual(len(requests), 1)

    def test_budget_exhaustion_requests_a_final_body_without_tools(self):
        history = tool_call("history", "git_history", "list")
        extra = tool_call("more", "git_changes", "files")
        for limits, initial_calls in (
            ({"max_tool_rounds": 1}, [history]),
            ({"max_tool_calls": 1}, [history]),
            ({"max_tool_calls": 1}, [history, extra]),
        ):
            with self.subTest(limits=limits, batch_size=len(initial_calls)):
                requests = []

                def request(url, service, *, payload, api_key):
                    requests.append(json.loads(json.dumps(payload)))
                    if len(requests) == 1:
                        return completion(*initial_calls)
                    if payload.get("tool_choice") == "none":
                        return completion(content=RELEASE_BODY)
                    return completion(extra)

                with patch.object(generate, "request_json", side_effect=request):
                    self.assertEqual(
                        generate.generate_notes(
                            self.repository, [], "https://example.invalid", "test-model", MOCK_KEY, **limits,
                        ),
                        RELEASE_BODY,
                    )
                self.assertEqual(len(requests), 2)
                self.assertEqual(requests[-1]["tool_choice"], "none")
                completed_calls = [
                    message["tool_call_id"] for message in requests[-1]["messages"] if message["role"] == "tool"
                ]
                self.assertEqual(completed_calls, ["history"] if len(initial_calls) == 1 else [])

    def test_model_cannot_return_or_forward_a_credential(self):
        echo = completion(content=f"{RELEASE_BODY}\n{MOCK_KEY}")
        search = tool_call("search", "git_source", "search", query=MOCK_KEY)
        arguments = search["function"]["arguments"]
        search["function"]["arguments"] = arguments.replace(MOCK_KEY, "\\u006d" + MOCK_KEY[1:])
        query = completion(search)
        for response in (echo, query):
            with self.subTest(response_type=response["choices"][0]["finish_reason"]), mock_chat(
                (200, {}, response),
            ) as (url, requests), patch.object(self.repository, "source") as source:
                with self.assertRaisesRegex(HarnessError, "A credential appeared") as error:
                    generate.generate_notes(self.repository, [], url, "test-model", MOCK_KEY)
                self.assertNotIn(MOCK_KEY, str(error.exception))
                self.assertEqual(len(requests), 1)
                source.assert_not_called()


if __name__ == "__main__":
    unittest.main()
