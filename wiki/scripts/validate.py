#!/usr/bin/env python3
"""Conformance checker for the OKF v0.1 bundle in wiki/.

Every file under wiki/ other than the reserved/scaffolding names below must
start with a YAML frontmatter block that includes a non-empty `type` field.
Repo scaffolding (README.md, AGENTS.md, CLAUDE.md, TEMPLATE.md,
WRITING_STYLE.md, requirements.txt, sources/, scripts/, .claude/) is never
scanned.
"""

import sys
from pathlib import Path

import yaml

RESERVED_NAMES = {"index.md", "log.md"}
SCAFFOLD_FILES = {"README.md", "AGENTS.md", "CLAUDE.md", "TEMPLATE.md", "WRITING_STYLE.md"}
SKIP_DIRS = {"sources", "scripts", ".claude", "node_modules", ".venv", "venv"}


def frontmatter(text: str):
    if not text.startswith("---\n"):
        return None
    end = text.find("\n---", 4)
    if end == -1:
        return None
    return text[4:end]


def find_concept_files(bundle_root: Path):
    for path in sorted(bundle_root.rglob("*.md")):
        rel = path.relative_to(bundle_root)
        if any(part in SKIP_DIRS for part in rel.parts):
            continue
        if path.name in RESERVED_NAMES:
            continue
        if rel.parent == Path(".") and path.name in SCAFFOLD_FILES:
            continue
        yield path


def check_concept(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    raw = frontmatter(text)
    if raw is None:
        return [f"{path}: missing YAML frontmatter block"]
    try:
        data = yaml.safe_load(raw) or {}
    except yaml.YAMLError as exc:
        return [f"{path}: invalid YAML frontmatter ({exc})"]
    if not isinstance(data, dict):
        return [f"{path}: frontmatter must be a mapping"]
    if not str(data.get("type") or "").strip():
        return [f"{path}: missing required non-empty 'type' field"]
    return []


def check_root_index(bundle_root: Path) -> list[str]:
    index = bundle_root / "index.md"
    if not index.exists():
        return ["wiki/index.md: missing bundle root index"]
    raw = frontmatter(index.read_text(encoding="utf-8"))
    if raw is None:
        return []
    try:
        yaml.safe_load(raw)
    except yaml.YAMLError as exc:
        return [f"wiki/index.md: invalid YAML frontmatter ({exc})"]
    return []


def main() -> int:
    bundle_root = Path(__file__).resolve().parent.parent
    if not bundle_root.is_dir():
        print("OKF validation failed: wiki/ directory is missing.")
        return 1

    errors = check_root_index(bundle_root)

    concept_files = list(find_concept_files(bundle_root))
    for path in concept_files:
        errors.extend(check_concept(path))

    if not (bundle_root / "log.md").exists():
        errors.append("wiki/log.md: missing bundle root log")

    if errors:
        print(f"OKF validation failed ({len(errors)} error(s)):")
        for error in errors:
            print(f"  - {error}")
        return 1

    print(f"OKF validation passed: {len(concept_files)} concept file(s) checked in wiki/.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
