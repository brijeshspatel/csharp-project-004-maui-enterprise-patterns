#!/usr/bin/env python3
"""Check that the catalogue and the pattern READMEs still agree.

The catalogue is this repository's contract: it names each pattern, the category it
belongs to, that category's gloss, and which patterns are done (a catalogue row that
links to a README) versus merely candidates (plain text). Three ways that contract goes
quietly wrong, none of which any other checker here can see:

* a pattern folder exists under patterns/ that no confirmed catalogue row links to, so
  the catalogue understates what the repository contains;
* a README's subtitle and its category's catalogue gloss drift apart, so two documents
  state the same thing differently and nothing reports the disagreement;
* a confirmed catalogue entry links to a README that does not exist, which the
  catalogue's own rule says means "done" -- a false claim about the repository's state.

`mdgs_check.py` cannot catch any of these: it grades placement, naming and frontmatter,
not agreement between two documents' prose.

Exit codes:
  0  every confirmed catalogue entry agrees with its pattern folder
  1  at least one disagreement
  2  nothing could be checked -- no catalogue, or no pattern folders. Never reported as
     a pass, because "nothing was checked" and "everything checked is clean" are
     different answers and only one of them is evidence.
"""

from __future__ import annotations

import pathlib
import re
import sys

CATALOGUE = pathlib.Path("docs/maui-enterprise-pattern-catalogue-v1.0.0.md")
PATTERNS = pathlib.Path("patterns")

CATEGORY_ROW = re.compile(r"^\|\s*\*\*(?P<category>[^*|]+)\*\*\s*\|\s*(?P<gloss>[^|]+?)\s*\|\s*$")
ENTRY_ROW = re.compile(r"^\|\s*(?P<category>[^|]+?)\s*\|\s*(?P<entry>.+?)\s*\|\s*(?P<status>[^|]+?)\s*\|\s*$")
LINK = re.compile(r"^\[(?P<text>[^\]]+)\]\((?P<target>[^)]+)\)$")


def parse_categories(text: str) -> dict[str, str]:
    """Category name -> gloss, from the '## Categories' table."""
    categories: dict[str, str] = {}
    in_section = False
    for line in text.splitlines():
        if line.strip() == "## Categories":
            in_section = True
            continue
        if in_section and line.startswith("## "):
            break
        if not in_section:
            continue
        match = CATEGORY_ROW.match(line)
        if match:
            categories[match.group("category")] = match.group("gloss")
    return categories


def parse_entries(text: str) -> list[dict[str, str | None]]:
    """Every entry row from the '## Entries' table."""
    entries: list[dict[str, str | None]] = []
    in_section = False
    for line in text.splitlines():
        if line.strip() == "## Entries":
            in_section = True
            continue
        if in_section and line.startswith("## "):
            break
        if not in_section:
            continue
        match = ENTRY_ROW.match(line)
        if not match:
            continue
        category = match.group("category")
        entry = match.group("entry")
        status = match.group("status")
        if category.startswith("---") or category == "Category":
            continue
        link = LINK.match(entry)
        target = None
        name = entry
        if link:
            target = link.group("target")
            name = link.group("text")
        entries.append({"category": category, "name": name, "target": target, "status": status})
    return entries


def readme_subtitle(readme: pathlib.Path) -> str | None:
    """The first bold line beneath the title, which is the subtitle by contract."""
    for line in readme.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if stripped.startswith("**"):
            return stripped
    return None


def main() -> int:
    if not CATALOGUE.is_file():
        print(f"CANNOT CHECK - no catalogue at {CATALOGUE}", file=sys.stderr)
        return 2
    if not PATTERNS.is_dir():
        print(f"CANNOT CHECK - no {PATTERNS}/ folder", file=sys.stderr)
        return 2

    folders = sorted(p for p in PATTERNS.iterdir() if p.is_dir())
    if not folders:
        print(f"CANNOT CHECK - no pattern folders under {PATTERNS}/", file=sys.stderr)
        return 2

    text = CATALOGUE.read_text(encoding="utf-8")
    categories = parse_categories(text)
    entries = parse_entries(text)

    problems: list[str] = []
    checked = 0
    linked_folders: set[str] = set()

    for row in entries:
        if row["target"] is None:
            continue  # a candidate row claims nothing to verify
        target = str(row["target"])
        resolved = (CATALOGUE.parent / target).resolve()
        if not resolved.is_file():
            problems.append(f"{row['name']}: catalogue links to {target}, which does not exist. "
                             "A linked entry means done")
            continue

        folder_name = resolved.parent.name
        linked_folders.add(folder_name)

        gloss = categories.get(str(row["category"]))
        if gloss is None:
            problems.append(f"{row['name']}: catalogue category '{row['category']}' has no "
                             "row in the Categories table")
            continue

        expected_subtitle = f"**{row['category']}** — {gloss}"
        subtitle = readme_subtitle(resolved)
        if subtitle is None:
            problems.append(f"{row['name']}: README has no subtitle beneath its title")
        elif subtitle != expected_subtitle:
            problems.append(
                f"{row['name']}: subtitle and catalogue category gloss differ\n"
                f"      README:    {subtitle}\n"
                f"      catalogue: {expected_subtitle}")
        checked += 1

    for folder in folders:
        if folder.name not in linked_folders:
            problems.append(f"{folder.name}: pattern folder present with no confirmed "
                             "catalogue row linking to it")

    if problems:
        print(f"pattern_relationship_check: {len(problems)} violation(s)")
        for problem in problems:
            print(f"  {problem}")
        return 1

    if checked == 0:
        print(f"CANNOT CHECK - {len(folders)} pattern folder(s) present, none linked from a "
              "confirmed catalogue entry", file=sys.stderr)
        return 2

    print(f"pattern_relationship_check: OK - {checked} confirmed entry checked, "
          f"{len(folders)} folder(s) present")
    return 0


if __name__ == "__main__":
    sys.exit(main())
