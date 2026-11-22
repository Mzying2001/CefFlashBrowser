"""Generate a GitHub Release body from the published base tag to local HEAD."""

import argparse
from http.client import HTTPException
import itertools
import json
import os
from pathlib import Path
import re
import sys
from urllib.error import HTTPError
from urllib.parse import unquote, urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener

from .repository import Git, HarnessError, RepositoryRange, github_repository


DEFAULT_API_BASE_URL = "https://api.deepseek.com"
DEFAULT_MODEL = "deepseek-flash"
PAGE_SIZE = 12_000
OVERVIEW_SIZE = 40_000
MAX_ROUNDS = 32
MAX_CALLS = 64
MAX_HTTP_BYTES = 8 * 1024 * 1024
TOKEN = re.compile(r"[A-Za-z0-9._~+/=-]+")


def tool(name, description, actions, **properties):
    return {
        "type": "function",
        "function": {
            "name": name,
            "description": description,
            "parameters": {
                "type": "object",
                "properties": {
                    "action": {"type": "string", "enum": actions},
                    "offset": {"type": "integer", "minimum": 0, "description": "Use next_offset to continue a result."},
                    **properties,
                },
                "required": ["action"],
                "additionalProperties": False,
            },
        },
    }


PATH_ARGUMENT = {"type": "string", "description": "Repository-relative path with forward slashes."}
TOOLS = [
    tool(
        "git_history", "List commits in the release range, or read one commit's full message.", ["list", "commit"],
        commit={"type": "string", "description": "Commit ID from the list. Required for commit."},
    ),
    tool(
        "git_changes", "List change statuses and filenames, or view the base-to-HEAD diff for a file.", ["files", "diff"],
        path={**PATH_ARGUMENT, "description": "Repository-relative file path, required for diff."},
    ),
    tool(
        "git_source", "Read a tracked text file or search tracked text for a literal string.", ["read", "search"],
        path={**PATH_ARGUMENT, "description": "File path for read, or optional file/directory for search."},
        query={"type": "string", "description": "Required for search. Single-line, case-sensitive literal text."},
        revision={"type": "string", "enum": ["base", "head"], "description": "Defaults to head."},
    ),
]


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def request_json(url, service, *, payload=None, api_key=None):
    headers = {"User-Agent": "CefFlashBrowser-ReleaseNotes"}
    data = None
    if api_key is None:
        headers["Accept"] = "application/vnd.github+json"
    else:
        body = json.dumps(payload)
        if api_key in body or api_key in unquote(url):
            raise HarnessError("A credential appeared in model request data.")
        headers.update({"Authorization": f"Bearer {api_key}", "Content-Type": "application/json"})
        data = body.encode("utf-8")

    try:
        request = Request(url, data=data, headers=headers)
        with build_opener(NoRedirect()).open(request, timeout=30 if api_key is None else 120) as response:
            data = response.read(MAX_HTTP_BYTES + 1)
    except HTTPError as error:
        error.close()
        raise HarnessError(f"{service} returned HTTP {error.code}.") from None
    except (OSError, HTTPException, ValueError):
        raise HarnessError(f"Could not connect to {service} or read its response.") from None

    if len(data) > MAX_HTTP_BYTES:
        raise HarnessError(f"{service} response exceeded the size limit.")
    try:
        return json.loads(data)
    except (ValueError, UnicodeError):
        raise HarnessError(f"{service} returned invalid JSON.") from None


def fetch_releases(repository):
    published = []
    for page in itertools.count(1):
        releases = request_json(
            f"https://api.github.com/repos/{repository}/releases?per_page=100&page={page}", "GitHub",
        )
        if not isinstance(releases, list):
            raise HarnessError("GitHub returned an invalid release list.")
        for release in releases:
            if not isinstance(release, dict):
                raise HarnessError("GitHub returned an invalid release record.")
            if release.get("draft") or release.get("prerelease") or not release.get("published_at"):
                continue
            if not all(isinstance(release.get(field), str) for field in ("tag_name", "published_at")):
                raise HarnessError("GitHub returned an invalid release record.")
            published.append(release)
        if len(releases) < 100:
            break
    published.sort(key=lambda release: release["published_at"], reverse=True)
    return [
        {"tag": release["tag_name"], "title": release.get("name") or "", "body": release.get("body") or ""}
        for release in published[:3]
    ]


def chat_endpoint(base_url):
    try:
        url = urlsplit(base_url)
        loopback = url.hostname in {"127.0.0.1", "::1", "localhost"}
        if (
            not url.hostname or url.username is not None or url.password is not None
            or url.query or url.fragment or (url.scheme != "https" and not (loopback and url.scheme == "http"))
        ):
            raise ValueError
        return f"{base_url.rstrip('/')}/chat/completions"
    except ValueError:
        raise HarnessError("Expected an HTTPS API base URL, or HTTP on loopback for testing.") from None


def page_text(text, offset, *, size=PAGE_SIZE):
    if type(offset) is not int or not 0 <= offset <= len(text):
        raise HarnessError("Expected an offset within the result. Start with offset 0.")
    end = min(offset + size, len(text))
    if end < len(text):
        newline = text.rfind("\n", offset, end)
        if newline >= offset:
            end = newline + 1
    return {"text": text[offset:end], "next_offset": end if end < len(text) else None}


def run_tool(repository, call, api_key):
    actions = {
        "git_history": repository.history,
        "git_changes": repository.changes,
        "git_source": repository.source,
    }
    function = call["function"]
    name = function.get("name")
    try:
        arguments = json.loads(function.get("arguments"))
    except (ValueError, TypeError):
        return {"error": "Tool arguments must be a JSON object."}
    if not isinstance(arguments, dict):
        return {"error": "Tool arguments must be a JSON object."}
    if api_key in json.dumps(arguments):
        raise HarnessError("A credential appeared in tool arguments.")
    try:
        if not isinstance(name, str) or name not in actions:
            raise HarnessError("Unknown tool. Use one of the advertised Git tools.")
        action = arguments.pop("action", None)
        offset = arguments.pop("offset", 0)
        if type(offset) is not int or offset < 0:
            raise HarnessError("Offset must be a nonnegative integer.")
        output = actions[name](action, arguments).replace(api_key, "[REDACTED]")
        return page_text(output, offset)
    except (HarnessError, UnicodeError) as error:
        message = str(error) if isinstance(error, HarnessError) else "Tool arguments must be valid Unicode text."
        return {"error": message}


def read_choice(response):
    try:
        choice = response["choices"][0]
        message = choice["message"]
        calls = message.get("tool_calls")
        if not isinstance(message, dict) or (calls is not None and not isinstance(calls, list)):
            raise TypeError
        for call in calls or []:
            if (
                not isinstance(call, dict) or not isinstance(call.get("id"), str) or not call["id"]
                or call.get("type") != "function" or not isinstance(call.get("function"), dict)
            ):
                raise TypeError
        return choice.get("finish_reason"), message, calls or []
    except (KeyError, IndexError, TypeError, AttributeError):
        raise HarnessError("The model API returned an invalid chat completion.") from None


def generate_notes(
    repository, references, api_base_url, model, api_key,
    *, max_tool_rounds=MAX_ROUNDS, max_tool_calls=MAX_CALLS,
):
    endpoint = chat_endpoint(api_base_url)
    try:
        system_prompt = Path(__file__).with_name("system_prompt.md").read_text(encoding="utf-8")
    except (OSError, UnicodeError):
        raise HarnessError("Could not read system_prompt.md as UTF-8.") from None

    commit_overview = repository.history("list", {}).replace(api_key, "[REDACTED]")
    changed_files = repository.changes("files", {}).replace(api_key, "[REDACTED]")
    context = {
        "base_tag": repository.tag,
        "base_commit": repository.base,
        "head_commit": repository.head,
        "commit_count": len(repository.commits),
        "commit_overview": page_text(commit_overview, 0, size=OVERVIEW_SIZE),
        "changed_files": page_text(changed_files, 0, size=OVERVIEW_SIZE),
        "prior_releases": references,
    }
    messages = [
        {"role": "system", "content": system_prompt},
        {"role": "user", "content": "Generate the release body for this range.\n" + json.dumps(context, ensure_ascii=False)},
    ]
    payload = {"model": model, "messages": messages, "tools": TOOLS}
    if urlsplit(endpoint).hostname == "api.deepseek.com":
        payload["thinking"] = {"type": "disabled"}

    calls_used = rounds_used = 0
    while True:
        final_request = rounds_used >= max_tool_rounds or calls_used >= max_tool_calls
        if final_request:
            payload["tool_choice"] = "none"
            messages.append({
                "role": "user",
                "content": "Write the final bilingual Markdown release body now, using the overviews and evidence already provided.",
            })
        response = request_json(endpoint, "the model API", payload=payload, api_key=api_key)
        finish, message, calls = read_choice(response)
        if finish in ("length", "content_filter"):
            raise HarnessError("The model API did not complete the release body.")
        content = message.get("content")
        if not calls:
            if not isinstance(content, str) or not content.strip():
                raise HarnessError("The model returned an empty release body.")
            if api_key in content:
                raise HarnessError("A credential appeared in the generated release body.")
            body = content.strip()
            headings = [line.strip() for line in body.splitlines() if line.strip() in {"### 中文", "### English"}]
            if headings != ["### 中文", "### English"] or body.startswith(("```", "~~~")):
                raise HarnessError("The model returned a release body in an unexpected format.")
            return body

        if final_request:
            raise HarnessError("The model requested tools after tool use was disabled for the final release body.")
        if api_key in json.dumps(message):
            raise HarnessError("A credential appeared in the model's tool request.")
        if calls_used + len(calls) > max_tool_calls:
            calls_used = max_tool_calls
            continue
        calls_used += len(calls)
        rounds_used += 1
        assistant = {"role": "assistant", "content": content, "tool_calls": calls}
        if isinstance(message.get("reasoning_content"), str):
            assistant["reasoning_content"] = message["reasoning_content"]
        messages.append(assistant)
        for call in calls:
            result = run_tool(repository, call, api_key)
            messages.append({"role": "tool", "tool_call_id": call["id"], "content": json.dumps(result, ensure_ascii=False)})


def load_api_key(path):
    try:
        key = path.read_text(encoding="utf-8-sig") if path else os.environ.get("RELEASE_NOTES_API_KEY", "")
    except (OSError, UnicodeError):
        raise HarnessError("Could not read the API key file as UTF-8.") from None
    key = key.strip()
    if not key or not TOKEN.fullmatch(key):
        raise HarnessError("Supply a valid API key with RELEASE_NOTES_API_KEY or --api-key-file.")
    return key


def positive_int(value):
    count = int(value)
    if count < 1:
        raise argparse.ArgumentTypeError("expected a positive integer")
    return count


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path.cwd(), help="Git repository directory (default: current directory)")
    parser.add_argument("--github-repo", metavar="OWNER/REPO", help="GitHub Release source (default: origin)")
    parser.add_argument("--base-tag", help="Local base tag (default: most recently published stable release)")
    parser.add_argument("--api-base-url", default=os.environ.get("RELEASE_NOTES_API_BASE_URL", DEFAULT_API_BASE_URL),
                        help="Chat Completions base URL (default: RELEASE_NOTES_API_BASE_URL or DeepSeek)")
    parser.add_argument("--model", default=os.environ.get("RELEASE_NOTES_MODEL", DEFAULT_MODEL),
                        help=f"Chat model (default: RELEASE_NOTES_MODEL or {DEFAULT_MODEL})")
    parser.add_argument("--api-key-file", type=Path, help="Read the API key from a file instead of RELEASE_NOTES_API_KEY")
    parser.add_argument(
        "--max-tool-rounds", type=positive_int, default=MAX_ROUNDS,
        help=f"Maximum rounds of tool use before writing the release body (default: {MAX_ROUNDS})",
    )
    parser.add_argument(
        "--max-tool-calls", type=positive_int, default=MAX_CALLS,
        help=f"Maximum individual tool calls across all rounds (default: {MAX_CALLS})",
    )
    args = parser.parse_args(argv)
    api_key = None
    try:
        api_key = load_api_key(args.api_key_file)
        git = Git(args.repo)
        head = git.text("rev-parse", "--verify", "HEAD^{commit}").strip()
        github = github_repository(args.github_repo) if args.github_repo else git.origin_repository()
        if api_key in github:
            raise HarnessError("A credential appeared in the GitHub repository name.")
        releases = fetch_releases(github)
        tag = args.base_tag or (releases[0]["tag"] if releases else None)
        if not tag:
            raise HarnessError("No stable GitHub Release found. Specify a local base tag with --base-tag.")
        if api_key in tag:
            raise HarnessError("A credential appeared in the base tag.")
        repository = RepositoryRange(git, tag, head)
        print(generate_notes(
            repository, releases, args.api_base_url, args.model, api_key,
            max_tool_rounds=args.max_tool_rounds, max_tool_calls=args.max_tool_calls,
        ))
    except (HarnessError, OSError, ValueError) as error:
        message = str(error) if isinstance(error, HarnessError) else "Could not generate release notes. Check the input and output settings."
        if api_key:
            message = message.replace(api_key, "[REDACTED]")
        print(f"Release notes: {message}", file=sys.stderr)
        return 1
    return 0
