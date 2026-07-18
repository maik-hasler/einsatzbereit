#!/usr/bin/env python3
"""Conformance checker for the wiki/bundle/ OKF v0.1 bundle.

The bundle is the `wiki/bundle/` subfolder: index.md, log.md, and the
concept pages. Every `.md` under it other than the reserved index.md/log.md
must start with a YAML frontmatter block that includes a non-empty `type`
field and a non-empty `tags` list, and must contain a `# Related` section
(even if its body is just "None found."). Everything at wiki/'s own root
(README.md, AGENTS.md, CLAUDE.md, TEMPLATE.md, WRITING_STYLE.md,
requirements.txt, scripts/) is scaffolding, lives outside the bundle, and is
never scanned.
"""

import re
import sys
from pathlib import Path

import yaml

RELATED_HEADING = re.compile(r"^# Related\s*$", re.MULTILINE)

RESERVED_NAMES = {"index.md", "log.md"}
SKIP_DIRS = {"node_modules", ".venv", "venv"}


def frontmatter(text: str):
    if not text.startswith("---\n"):
        return None
    end = text.find("\n---", 4)
    if end == -1:
        return None
    return text[4:end]


def read_normalized(path: Path) -> str:
    """Read a file as UTF-8 and normalize CRLF/CR to LF, so frontmatter and
    heading detection work on Windows-authored pages too."""
    return path.read_text(encoding="utf-8").replace("\r\n", "\n").replace("\r", "\n")


def find_concept_files(bundle_root: Path):
    for path in sorted(bundle_root.rglob("*.md")):
        rel = path.relative_to(bundle_root)
        if any(part in SKIP_DIRS for part in rel.parts):
            continue
        if path.name in RESERVED_NAMES:
            continue
        yield path


def check_concept(path: Path) -> list[str]:
    text = read_normalized(path)
    raw = frontmatter(text)
    if raw is None:
        return [f"{path}: missing YAML frontmatter block"]
    try:
        data = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        return [f"{path}: invalid YAML frontmatter ({exc})"]
    if not isinstance(data, dict):
        return [f"{path}: frontmatter must be a mapping"]

    errors = []
    if not str(data.get("type") or "").strip():
        errors.append(f"{path}: missing required non-empty 'type' field")
    tags = data.get("tags")
    if not isinstance(tags, list) or not [t for t in tags if str(t).strip()]:
        errors.append(f"{path}: missing required non-empty 'tags' list")
    if not RELATED_HEADING.search(text):
        errors.append(
            f"{path}: missing required '# Related' section "
            f"(state the links found, or write 'None found.')"
        )
    return errors


def check_root_index(bundle_root: Path) -> list[str]:
    index = bundle_root / "index.md"
    if not index.exists():
        return ["wiki/bundle/index.md: missing bundle root index"]
    raw = frontmatter(read_normalized(index))
    if raw is None:
        return []
    try:
        yaml.safe_load(raw)
    except yaml.YAMLError as exc:
        return [f"wiki/bundle/index.md: invalid YAML frontmatter ({exc})"]
    return []


def main() -> int:
    bundle_root = Path(__file__).resolve().parent.parent / "bundle"
    if not bundle_root.is_dir():
        print("OKF validation failed: wiki/bundle/ directory is missing.")
        return 1

    errors = check_root_index(bundle_root)

    concept_files = list(find_concept_files(bundle_root))
    for path in concept_files:
        errors.extend(check_concept(path))

    if not (bundle_root / "log.md").exists():
        errors.append("wiki/bundle/log.md: missing bundle root log")

    if errors:
        print(f"OKF validation failed ({len(errors)} error(s)):")
        for error in errors:
            print(f"  - {error}")
        return 1

    print(f"OKF validation passed: {len(concept_files)} concept file(s) checked in wiki/bundle/.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
