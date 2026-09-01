#!/usr/bin/env python3
"""Regenerate german-postal-codes.json from the GeoNames export.

Two GeoNames files feed the result:

* export/zip/DE.zip - German postal codes with a place name and coordinates.
  It also lists "Grossempfaenger" codes reserved for a single company
  (e.g. 70140 "Commerzbank AG"), which nobody searches for a volunteering
  opportunity.
* export/dump/DE.zip - every populated place and municipality in Germany.
  A postal code is kept only when its place name appears here, which is what
  separates a real town from a company mailbox.

Both are GeoNames.org data under CC BY 4.0; see README.md next to the output.

Usage: python3 generate-german-postal-codes.py [--out ../german-postal-codes.json]
"""

import argparse
import io
import json
import pathlib
import unicodedata
import urllib.request
import zipfile

POSTAL_CODES_URL = "https://download.geonames.org/export/zip/DE.zip"
POPULATED_PLACES_URL = "https://download.geonames.org/export/dump/DE.zip"

# Feature codes that make a GeoNames row a place people live in: the "P" class
# covers cities, towns and villages, ADM4/ADM5 add the municipalities that carry
# no separate populated-place row of their own (e.g. Heidenrod).
POPULATED_PLACE_CLASS = "P"
MUNICIPALITY_CLASSES = {("A", "ADM4"), ("A", "ADM5")}

COORDINATE_PRECISION = 4


def normalize(value):
    """Fold a place name the way GermanPlaceNameNormalizer.cs does."""
    folded = (
        value.lower()
        .replace("ß", "ss")
        .replace("ä", "ae")
        .replace("ö", "oe")
        .replace("ü", "ue")
    )
    decomposed = unicodedata.normalize("NFD", folded)
    return "".join(c for c in decomposed if unicodedata.category(c) != "Mn")


def download_rows(url):
    """Yield the tab-separated rows of the single DE.txt inside a GeoNames zip."""
    with urllib.request.urlopen(url) as response:
        archive = zipfile.ZipFile(io.BytesIO(response.read()))
    with archive.open("DE.txt") as data:
        for line in io.TextIOWrapper(data, encoding="utf-8"):
            yield line.rstrip("\n").split("\t")


def load_populated_places():
    """Map every known spelling of a German place to its largest population."""
    populations = {}
    for row in download_rows(POPULATED_PLACES_URL):
        feature_class, feature_code = row[6], row[7]
        if feature_class != POPULATED_PLACE_CLASS and (feature_class, feature_code) not in MUNICIPALITY_CLASSES:
            continue
        population = int(row[14] or 0)
        names = [row[1], row[2]] + (row[3].split(",") if row[3] else [])
        for name in names:
            key = normalize(name.strip())
            if key and population > populations.get(key, -1):
                populations[key] = population
    return populations


def group_postal_codes():
    """Group the postal-code export by code, keeping name and coordinates."""
    grouped = {}
    for row in download_rows(POSTAL_CODES_URL):
        postal_code, place_name = row[1], row[2]
        grouped.setdefault(postal_code, []).append((place_name, float(row[9]), float(row[10])))
    return grouped


def build_entries():
    populations = load_populated_places()
    entries = []
    for postal_code, rows in sorted(group_postal_codes().items()):
        known = [row for row in rows if normalize(row[0]) in populations]
        if not known:
            continue
        # A code often lists both the municipality and its districts ("Dresden",
        # "Dresden Friedrichstadt"). The most populated one is the name people
        # recognise, so it becomes the label.
        place_name = max(known, key=lambda row: populations[normalize(row[0])])[0]
        matching = [row for row in known if row[0] == place_name]
        entries.append(
            {
                "z": postal_code,
                "n": place_name,
                "lat": round(sum(row[1] for row in matching) / len(matching), COORDINATE_PRECISION),
                "lon": round(sum(row[2] for row in matching) / len(matching), COORDINATE_PRECISION),
            }
        )
    return entries


def main():
    default_out = pathlib.Path(__file__).resolve().parent.parent / "german-postal-codes.json"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", type=pathlib.Path, default=default_out)
    args = parser.parse_args()

    entries = build_entries()
    args.out.write_text(
        json.dumps(entries, ensure_ascii=False, separators=(",", ":")) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {len(entries)} postal codes to {args.out}")


if __name__ == "__main__":
    main()
