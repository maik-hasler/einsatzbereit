#!/usr/bin/env python3
"""Report comment density across the repository's hand-written sources.

CONTRIBUTING.md's Code Style asks that comments explain *why*, not *what*.
This script measures the resulting ratio so the trend stays visible: a file
drifting toward a high ratio is usually one that has started narrating its
own code rather than recording the reasoning behind it.

Coverage spans code and the configuration around it: C-style sources
(.cs/.ts/.tsx/.js/.css), #-commented config (.yml/.yaml/.sh), AsciiDoc,
PlantUML, FreeMarker templates and Markdown. CI workflows and shell scripts
are included on purpose - they are opaque enough to attract narration, and
leaving them out hid the two densest file types in the repository.

C-style sources get a character-precise scan that tracks string, char and
template state, so a "//" inside a literal is not miscounted; the remaining
formats are scanned line by line, which is enough where a comment marker
occupies the whole line.

Generated and vendored files are excluded - the NSwag clients, EF Core
migrations, the pnpm lockfile and the vendored frontend-design skill. Their
comment volume says nothing about how the team writes code.

Usage:
    python3 scripts/comment-density.py            # repository summary
    python3 scripts/comment-density.py --top 20   # worst offenders by ratio
    python3 scripts/comment-density.py --json     # machine-readable output
"""

import argparse
import json
import os
import subprocess
import sys
from collections import defaultdict

# Comment syntax per extension. "c" is the character-precise scanner below;
# the rest are line-oriented, which is enough for config and markup where a
# comment marker occupies the whole line.
C_STYLE = {".cs", ".ts", ".tsx", ".js", ".css"}
HASH_STYLE = {".yml", ".yaml", ".sh"}
ADOC_STYLE = {".adoc"}
PUML_STYLE = {".puml"}
WRAPPED_STYLE = {".ftl": ("<#--", "-->"), ".md": ("<!--", "-->")}

EXTENSIONS = (
    C_STYLE | HASH_STYLE | ADOC_STYLE | PUML_STYLE | set(WRAPPED_STYLE)
)

GENERATED_FILES = {
    "backend/src/Api/Api.cs",
    "backend/tests/IntegrationTests/ApiClient.cs",
    "frontend/src/client/api-client.ts",
    "frontend/pnpm-lock.yaml",
}

# Third-party content that is checked in but not written here: the vendored
# frontend-design skill (Apache-2.0, from anthropics/skills).
VENDORED_PREFIXES = (".claude/skills/frontend-design/",)


def is_generated(path):
    if path in GENERATED_FILES or "/Migrations/" in path:
        return True
    return path.startswith(VENDORED_PREFIXES)


def scan(text):
    """Classify each line as holding code and/or comment content."""
    lines = text.split("\n")
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)

    i, line, state = 0, 0, "code"
    length = len(text)
    while i < length:
        char = text[i]
        nxt = text[i + 1] if i + 1 < length else ""

        if char == "\n":
            if state == "line_comment":
                state = "code"
            line += 1
            i += 1
            continue

        if state == "code":
            if char == "/" and nxt == "/":
                state = "line_comment"
                has_comment[line] = True
                i += 2
                continue
            if char == "/" and nxt == "*":
                state = "block_comment"
                has_comment[line] = True
                i += 2
                continue
            if char in "'\"`":
                state = {"'": "sq", '"': "dq", "`": "bq"}[char]
            if not char.isspace():
                has_code[line] = True
            i += 1
            continue

        if state == "line_comment":
            i += 1
            continue

        if state == "block_comment":
            has_comment[line] = True
            if char == "*" and nxt == "/":
                state = "code"
                i += 2
                continue
            i += 1
            continue

        # inside a string literal
        has_code[line] = True
        if char == "\\":
            if nxt == "\n":
                line += 1
            i += 2
            continue
        if (
            (state == "sq" and char == "'")
            or (state == "dq" and char == '"')
            or (state == "bq" and char == "`")
        ):
            state = "code"
        i += 1

    return lines, has_code, has_comment


def scan_hash(text):
    """Line-oriented scanner for #-commented config (YAML, shell)."""
    lines = text.split("\n")
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)
    for index, raw in enumerate(lines):
        quote = ""
        for position, char in enumerate(raw):
            if quote:
                if char == quote:
                    quote = ""
                continue
            if char in "'\"":
                quote = char
                continue
            if char == "#":
                has_comment[index] = True
                has_code[index] = bool(raw[:position].strip())
                break
        else:
            has_code[index] = bool(raw.strip())
    return lines, has_code, has_comment


def scan_prefix(text, prefix, block=None):
    """Line-oriented scanner for markup whose comments start a line.

    `block` is an optional fence (AsciiDoc's ////) that toggles a comment
    region on and off.
    """
    lines = text.split("\n")
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)
    inside = False
    for index, raw in enumerate(lines):
        stripped = raw.strip()
        if not stripped:
            continue
        if block and stripped.startswith(block):
            inside = not inside
            has_comment[index] = True
            continue
        if inside or stripped.startswith(prefix):
            has_comment[index] = True
        else:
            has_code[index] = True
    return lines, has_code, has_comment


def scan_wrapped(text, opener, closer):
    """Line-oriented scanner for <!-- --> / <#-- --> style comments."""
    lines = text.split("\n")
    has_code = [False] * len(lines)
    has_comment = [False] * len(lines)
    inside = False
    for index, raw in enumerate(lines):
        stripped = raw.strip()
        if not stripped:
            continue
        remainder = stripped
        saw_comment = False
        saw_code = False
        while remainder:
            if inside:
                end = remainder.find(closer)
                saw_comment = True
                if end == -1:
                    remainder = ""
                else:
                    inside = False
                    remainder = remainder[end + len(closer):]
                continue
            start = remainder.find(opener)
            if start == -1:
                saw_code = saw_code or bool(remainder.strip())
                break
            if remainder[:start].strip():
                saw_code = True
            inside = True
            saw_comment = True
            remainder = remainder[start + len(opener):]
        has_comment[index] = saw_comment
        has_code[index] = saw_code
    return lines, has_code, has_comment


def scan_for(path, text):
    ext = os.path.splitext(path)[1]
    if ext in C_STYLE:
        return scan(text)
    if ext in HASH_STYLE:
        return scan_hash(text)
    if ext in ADOC_STYLE:
        return scan_prefix(text, "//", block="////")
    if ext in PUML_STYLE:
        return scan_prefix(text, "'")
    opener, closer = WRAPPED_STYLE[ext]
    return scan_wrapped(text, opener, closer)


def measure(path):
    try:
        text = open(path, encoding="utf-8").read()
    except (OSError, UnicodeDecodeError):
        return None

    lines, has_code, has_comment = scan_for(path, text)
    code = comment = mixed = 0
    for index, raw in enumerate(lines):
        if not raw.strip():
            continue
        if has_comment[index] and has_code[index]:
            mixed += 1
        elif has_comment[index]:
            comment += 1
        elif has_code[index]:
            code += 1

    nonblank = code + comment + mixed
    return {
        "path": path,
        "ext": os.path.splitext(path)[1],
        "code": code,
        "comment": comment,
        "mixed": mixed,
        "nonblank": nonblank,
        "ratio": (comment + mixed) / nonblank if nonblank else 0.0,
    }


def collect():
    listing = subprocess.check_output(["git", "ls-files"], text=True)
    results = []
    for path in listing.split("\n"):
        if not path or os.path.splitext(path)[1] not in EXTENSIONS:
            continue
        if is_generated(path):
            continue
        stats = measure(path)
        if stats:
            results.append(stats)
    return results


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--top", type=int, default=0,
                        help="list the N densest files (>=40 non-blank lines)")
    parser.add_argument("--json", action="store_true",
                        help="emit machine-readable JSON")
    args = parser.parse_args()

    files = collect()
    if not files:
        print("no source files matched", file=sys.stderr)
        return 1

    code = sum(f["code"] for f in files)
    comment = sum(f["comment"] + f["mixed"] for f in files)
    nonblank = code + comment

    if args.json:
        summary = {
            "totals": {
                "code": code,
                "comment": comment,
                "ratio": comment / nonblank,
            },
            "files": files,
        }
        json.dump(summary, sys.stdout, indent=1)
        print()
        return 0

    print(f"files          {len(files)}")
    print(f"code lines     {code}")
    print(f"comment lines  {comment}")
    print(
        f"density        {comment / nonblank * 100:.2f}%  "
        f"(comment lines / non-blank lines)"
    )

    by_ext = defaultdict(lambda: defaultdict(int))
    for f in files:
        bucket = by_ext[f["ext"]]
        bucket["code"] += f["code"]
        bucket["comment"] += f["comment"] + f["mixed"]
    print("\nby extension")
    for ext, bucket in sorted(by_ext.items(), key=lambda kv: -kv[1]["comment"]):
        total = bucket["code"] + bucket["comment"]
        print(
            f"  {ext:5} {bucket['comment']:6} / {total:7}  "
            f"{bucket['comment'] / total * 100:5.2f}%"
        )

    if args.top:
        print(f"\ndensest {args.top} files (>=40 non-blank lines)")
        ranked = [f for f in files if f["nonblank"] >= 40]
        ranked.sort(key=lambda f: -f["ratio"])
        for f in ranked[: args.top]:
            print(f"  {f['ratio'] * 100:5.1f}%  {f['path']}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
