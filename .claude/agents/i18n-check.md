---
name: i18n-check
description: Checks whether a change to frontend/src/locales/en.json or de.json left the two files out of sync (a key added/renamed/removed in one but not the other). Use proactively after editing either locale file, or after adding a new translation key reference (t("...")) in a component.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

`frontend/src/locales/en.json` and `de.json` share one nested key structure
under `translation` (`i18next` resources, wired up in `frontend/src/i18n.ts`)
- and CI does compare them: `frontend-checks.yml` runs `pnpm i18n:check`
(`frontend/scripts/check-i18n-keys.js`), which enforces key parity plus
identical-value, placeholder, plural and unused-key rules. `eslint-plugin-i18next`
is configured only with the `no-literal-string` rule, so it catches hardcoded
JSX strings, not key parity.

This agent is the local pre-flight for that CI job: it reads the diff and names
the offending key before a push spends a CI round on it. It is not the only
gate, so never report "nothing else checks this" - if you want certainty, run
`pnpm i18n:check` from `frontend/` yourself.

Compare the current diff (`git diff -- frontend/src/locales`):

- A key added to one file but not the other.
- A key renamed/removed in one file but left stale in the other.
- Structural drift - a key that's a nested object in one file and a leaf
  string in the other.

A quick way to confirm programmatically (flatten both to dotted-path key
sets and diff them):

```bash
python3 -c "
import json
def flatten(d, prefix=''):
    keys = set()
    for k, v in d.items():
        p = f'{prefix}.{k}' if prefix else k
        keys |= flatten(v, p) if isinstance(v, dict) else {p}
    return keys
en = flatten(json.load(open('frontend/src/locales/en.json'))['translation'])
de = flatten(json.load(open('frontend/src/locales/de.json'))['translation'])
print('in en, missing from de:', sorted(en - de))
print('in de, missing from en:', sorted(de - en))
"
```

Also flag a new `t("some.key")` call in a `.tsx`/`.ts` diff that doesn't
resolve to an existing key in *either* locale file.

Report only - never edit the locale files yourself.
