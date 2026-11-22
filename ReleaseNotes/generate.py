"""Run the release notes generator from a source checkout."""

import sys

from src.release_notes.generate import main


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8", newline="\n")
    raise SystemExit(main())
