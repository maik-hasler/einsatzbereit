---
type: <concept type>               # required
title: <title>                      # recommended
description: <one-line summary>     # recommended
resource: <url>                     # recommended
tags: [<tag>, <tag>]                 # required - drives the Related-section search, see AGENTS.md
timestamp: <ISO 8601 datetime>       # recommended
# status: <draft|verified|stale>   (optional custom field, not core OKF -
#   lets a lint pass find stale pages by lookup instead of by re-reading them)
# superseded_by: <path/to/newer-concept.md>   (optional - set instead of
#   deleting this page when a new source contradicts it; keep the old claim)
---

# Schema

# Examples

# Related
<!-- bundle-relative links to other concept pages this one connects to (a
     leading `/` resolves from wiki/, the bundle root). Required on every
     page - if the search in AGENTS.md's Ingest step 5 genuinely finds
     nothing, write "None found." rather than leaving this blank. -->

# Citations
<!-- one entry per source used: `docs/notes/<path>` (loose notes), a repo
     path optionally with `@<commit-sha>` (code/AGENTS.md or CLAUDE.md/hooks),
     or `#NNN` / a full issue-or-PR URL (GitHub) -->
