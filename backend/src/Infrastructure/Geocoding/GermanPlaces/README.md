# German place data

Two embedded gazetteers back the location search-as-you-type field. Both are
derived from [GeoNames.org](https://www.geonames.org/) data, licensed under
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).

| File | Rows | Content | Source |
| --- | --- | --- | --- |
| `german-cities.json` | 3,079 | German places with a population of 5,000 or more: name, coordinates, population | GeoNames `export/dump/DE.zip` |
| `german-postal-codes.json` | 8,302 | German postal codes with the place they belong to and its coordinates | GeoNames `export/zip/DE.zip`, validated against `export/dump/DE.zip` |

Keys are single letters (`n`, `lat`, `lon`, `p`, `z`) and the files ship
minified, because they are read once at startup and every byte lands in the
published container image.

## Regenerating the postal codes

```bash
python3 tools/generate-german-postal-codes.py
```

The script downloads both GeoNames exports and keeps a postal code only when
its place name is a populated place or a municipality. That filter is what
drops the roughly 2,500 "Grossempfaenger" codes reserved for a single company
(70140 is `Commerzbank AG`, not a town). Nominatim still resolves those, since
`NominatimGeocodingService` falls back to it whenever these files come up empty.

`german-cities.json` has no generator: its German spellings were picked from
the GeoNames alternate names by hand in #2227, and the raw `name` column would
regress them (`Munich` instead of `München`).
