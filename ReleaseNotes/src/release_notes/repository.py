"""Inspect committed repository data through a fixed set of Git commands."""

import os
from pathlib import Path
import re
import shutil
import subprocess
from urllib.parse import urlsplit


class HarnessError(Exception):
    """An error that can be reported without printing command or API payloads."""


EXCLUDED_PATHS = (":(top,exclude,icase).git", ":(top,exclude,icase)local-notes")
MAX_SOURCE_BYTES = 2 * 1024 * 1024
GITHUB_REPO = re.compile(r"[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+")
# A NUL-delimited pipe also preserves braces and glob characters on Git for Windows.
GIT_SCRIPT = '''args=()
while IFS= read -r -d '' arg; do args+=("$arg"); done
exec git "${args[@]}"
'''


def github_repository(value):
    if not GITHUB_REPO.fullmatch(value) or value.split("/")[-1] in {".", ".."}:
        raise HarnessError("Expected a GitHub repository in OWNER/REPO format.")
    return value


def find_bash():
    if os.name == "nt" and (git := shutil.which("git")):
        # A typical Git for Windows installation adds only its cmd directory to PATH.
        for parent in Path(git).parents:
            candidate = parent / "usr" / "bin" / "bash.exe"
            if candidate.is_file():
                return str(candidate)
    if bash := shutil.which("bash"):
        return bash
    raise HarnessError("Bash was not found. Install Git for Windows or add Bash to PATH.")


class Git:
    def __init__(self, directory):
        self.bash = find_bash()
        self.directory = directory
        inherited = {"PATH", "SYSTEMROOT", "WINDIR", "TEMP", "TMP", "TMPDIR", "LANG", "LC_ALL"}
        self.env = {name: value for name, value in os.environ.items() if name.upper() in inherited}
        self.env.update({
            "GIT_CONFIG_NOSYSTEM": "1",
            "GIT_CONFIG_GLOBAL": os.devnull,
            "GIT_NO_LAZY_FETCH": "1",
            "GIT_ALLOW_PROTOCOL": "",
            "GIT_TERMINAL_PROMPT": "0",
            "GIT_OPTIONAL_LOCKS": "0",
            "MSYS2_ARG_CONV_EXCL": "*",
        })

        self.directory = Path(self.text("rev-parse", "--show-toplevel").rstrip("\r\n"))

    def run(self, command, *args, allowed=(0,)):
        git_args = (
            "--no-pager", "--no-replace-objects", "-c", "core.quotePath=false", command, *args,
        )
        if any("\0" in arg for arg in git_args):
            raise HarnessError("Git arguments cannot contain NUL characters.")
        data = ("\0".join(git_args) + "\0").encode("utf-8")
        try:
            result = subprocess.run(
                [self.bash, "--noprofile", "--norc", "-c", GIT_SCRIPT],
                cwd=self.directory, env=self.env, input=data,
                capture_output=True, timeout=15, check=False,
            )
        except (OSError, subprocess.TimeoutExpired):
            raise HarnessError(f"Could not run git {command} within 15 seconds.") from None
        if result.returncode not in allowed:
            raise HarnessError(f"git {command} failed (exit {result.returncode}).")
        return result

    def text(self, command, *args, allowed=(0,)):
        return self.run(command, *args, allowed=allowed).stdout.decode("utf-8", errors="replace")

    def origin_repository(self):
        origin = self.text("remote", "get-url", "origin").strip()
        if origin.startswith("git@github.com:"):
            path = origin.removeprefix("git@github.com:")
        else:
            parsed = urlsplit(origin)
            if parsed.scheme not in {"http", "https", "ssh"} or parsed.hostname != "github.com":
                raise HarnessError("The origin is not on GitHub. Use --github-repo OWNER/REPO.")
            path = parsed.path.strip("/")
        return github_repository(path.removesuffix(".git"))


def literal_path(path):
    if not isinstance(path, str) or not path or len(path) > 1024:
        raise HarnessError("Expected a repository-relative path.")
    parts = path.split("/")
    if (
        any(char in path for char in "\\:") or any(ord(char) < 32 for char in path)
        or any(part in {"", ".", ".."} or part.casefold() == ".git" for part in parts)
        or parts[0].casefold() == "local-notes"
    ):
        raise HarnessError("Path must point to allowed content in the repository.")
    return f":(top,literal){path}"


class RepositoryRange:
    def __init__(self, git, tag, head):
        self.git = git
        self.head = head
        self.tag = tag
        ref = f"refs/tags/{tag}"
        try:
            git.run("check-ref-format", ref)
            self.base = git.text("rev-parse", "--verify", "--end-of-options", f"{ref}^{{commit}}").strip()
        except HarnessError:
            raise HarnessError(f"Cannot resolve local base tag {tag!r}. Verify the tag or fetch it first.") from None
        if self.base == self.head:
            raise HarnessError("The base tag and HEAD point to the same commit.")
        if git.run("merge-base", "--is-ancestor", self.base, self.head, allowed=(0, 1)).returncode:
            raise HarnessError("The base tag is not an ancestor of HEAD. Check the branch and fetch full history if shallow.")
        self.commits = git.text(
            "log", "--no-show-signature", "--encoding=UTF-8", "--format=%H%x09%s", f"{self.base}..{self.head}", "--",
        ).splitlines()

    def history(self, action, args):
        if action == "list" and not args:
            return "\n".join(self.commits)
        if action == "commit" and args.keys() == {"commit"}:
            prefix = args["commit"]
            if not isinstance(prefix, str) or not re.fullmatch(r"[0-9a-f]{7,64}", prefix):
                raise HarnessError("Expected a hexadecimal commit ID from git_history.")
            matches = [line.split("\t", 1)[0] for line in self.commits if line.startswith(prefix)]
            if len(matches) != 1:
                raise HarnessError("Commit ID must identify one commit in the release range.")
            return self.git.text(
                "show", "--no-patch", "--no-show-signature", "--encoding=UTF-8", "--format=%H%n%s%n%n%b", matches[0], "--",
            )
        raise HarnessError("Use list or commit with the corresponding arguments.")

    def changes(self, action, args):
        options = ("--no-ext-diff", "--no-textconv", "--no-color", "--no-relative", "--submodule=short")
        if action == "files" and not args:
            return self.git.text(
                "diff", *options, "--name-status", "--find-renames", self.base, self.head, "--", *EXCLUDED_PATHS,
            )
        if action == "diff" and args.keys() == {"path"}:
            pathspec = literal_path(args["path"])
            return self.git.text("diff", *options, self.base, self.head, "--", pathspec, *EXCLUDED_PATHS)
        raise HarnessError("Use files or diff with the corresponding arguments.")

    def source(self, action, args):
        revision = args.pop("revision", "head")
        if revision not in ("base", "head"):
            raise HarnessError("Revision must be base or head.")
        commit = self.base if revision == "base" else self.head
        if action == "read" and args.keys() == {"path"}:
            path = args["path"]
            literal_path(path)
            obj = f"{commit}:{path}"
            if self.git.text("cat-file", "-t", obj).strip() != "blob":
                raise HarnessError("The requested path must be a file.")
            if int(self.git.text("cat-file", "-s", obj)) > MAX_SOURCE_BYTES:
                raise HarnessError("The requested file exceeds the 2 MiB source limit.")
            data = self.git.run("cat-file", "blob", obj).stdout
            if b"\x00" in data:
                raise HarnessError("The requested file is binary.")
            return data.decode("utf-8", errors="replace")
        if action == "search" and args.keys() in ({"query"}, {"query", "path"}):
            query = args["query"]
            if (
                not isinstance(query, str) or not 1 <= len(query) <= 200
                or any(ord(char) < 32 for char in query)
            ):
                raise HarnessError("Expected a single-line literal query of 1–200 characters.")
            scope = [literal_path(args["path"])] if "path" in args else []
            return self.git.text(
                "grep", "-n", "-I", "-F", "-e", query, commit, "--", *scope, *EXCLUDED_PATHS, allowed=(0, 1),
            )
        raise HarnessError("Use read or search with the corresponding arguments.")
