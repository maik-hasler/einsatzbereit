# Writing style

Prose rule for every write to `wiki/` - ingest, query, lint, or any ad hoc
edit. Goal: read like a knowledgeable human wrote it, not a chatbot. Based on
[Wikipedia: Signs of AI writing](https://en.wikipedia.org/wiki/Wikipedia:Signs_of_AI_writing).

These are signals, not proof - human writing can contain them too. The point
isn't a witch-hunt, it's to write plainly and cut the tells below on sight.

## 1. Cut puffery and editorializing

State facts; let them stand without inflation.

- Banned phrases: "stands as a testament", "rich tapestry", "rich (cultural) heritage", "enduring legacy", "plays a vital/crucial/pivotal role", "leaves a lasting impact", "a must-(see/read)", "nestled in", "boasts".
- Banned meta-commentary: "it's important to note", "it's worth noting", "it should be remembered", "no discussion would be complete without", "needless to say".
- No editorial adjectives unless sourced: "breathtaking", "vibrant", "renowned", "celebrated", "fascinating", "powerful".

## 2. Drop the LLM vocabulary

Overused tell-words - prefer the plain alternative:

| Avoid | Use |
|---|---|
| delve into | look at, examine |
| showcase | show |
| underscore / highlight | stress, point to |
| foster | support, encourage |
| leverage | use |
| navigate (the landscape/realm of) | handle, deal with |
| realm, landscape, tapestry, sphere | (name the actual thing) |
| robust, seamless, comprehensive, holistic | (be specific or cut) |
| crucial, pivotal, key (as filler) | (cut, or say why) |

## 3. Kill the AI sentence patterns

- No "Not only X, but also Y."
- No negative parallelism: "It's not just X - it's Y."
- No forced rule-of-three triads ("fast, reliable, and scalable") unless each word earns its place.
- No summarizing wrap-ups: "In conclusion", "Overall", "In summary", "Ultimately".
- No vague attribution: "industry experts say", "studies show", "it is widely regarded" - name the source or cut the claim.

## 4. Formatting restraint

- **Bold** only for true key terms - never scattered through sentences for emphasis.
- Prefer prose over bullet lists. Use a list only for genuinely discrete items; never a list of one or two.
- No section header for a one-line section. No "Overview" / "Conclusion" headers on short pages.
- No Unicode en/em dashes anywhere in this repo (root `AGENTS.md`, CI enforced) - use a plain ASCII hyphen, or restructure the sentence instead of leaning on a dash as an all-purpose connector.
- Headings in sentence case, not Title Case. No emoji.
- No leftover markdown artifacts, no curly "smart" quotes pasted from elsewhere.

## 5. Citations and facts

- Every non-obvious claim is verifiable; cite the specific source - `docs/notes/<path>`, a repo path (optionally `@<commit-sha>`), or `#NNN` / a full issue-or-PR URL. Never invent a reference, commit SHA, issue number, or date.
- If a fact is unverified, say so or leave it out - don't hedge it into existence ("some sources suggest").
- Match the claim to what the source actually says.

## Pre-publish checklist

Before saving a page, scan for: puffery phrases (1), tell-words (2), the
four sentence patterns (3), scattered bold / needless lists / stray Unicode
dashes (4), any unsourced or invented claim (5), and a `# Related` section
that's missing, blank, or says "None found" without an actual grep behind
it (cross-check `AGENTS.md`'s Ingest step 5). Cut what you find.
