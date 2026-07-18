---
type: "reference"
title: "Index of formal ADRs and TDRs"
description: "Pointer to the reviewed architecture decisions and technical-debt records under docs/ - link these, do not restate them in the wiki."
tags:
  - adr
  - clean-architecture
  - keycloak
  - rate-limiting
  - wiki
timestamp: 2026-07-18
---

# Index of formal ADRs and TDRs

The reviewed decisions live under `docs/` as arc42-style AsciiDoc, not in this wiki. This page is a pointer so agents find the authoritative record fast. When a topic is already an ADR or TDR, link the `.adoc` file; do not copy its content into a wiki page, which would then drift.

Trust the directory listing, not the summary. The structure block in `docs/AGENTS.md` stops at ADR-3, but `docs/ADRs/` also contains ADR-4. Run `ls docs/ADRs docs/TDRs` for the real set.

## Architecture decisions (docs/ADRs/)

- **ADR-1** `1_monorepository.adoc` (Accepted 2026-03-23) - one repo for backend, frontend, keycloak, docs. Cross-component changes land in a single PR; CI is conditional per component.
- **ADR-2** `2_arc42.adoc` (Accepted 2026-03-25) - arc42 template with C4 diagrams as PlantUML source. This is why ADRs are referenced from arc42 chapter 9 and the docs are AsciiDoc, not Markdown.
- **ADR-3** `3_keycloak.adoc` (Accepted 2026-03-25) - self-hosted Keycloak for IAM. The driving constraint is no dependency on US-based cloud providers (data sovereignty) plus no license cost; Auth0 and Entra ID were rejected on those grounds. Realm config is version-controlled as a JSON export in the custom Docker image.
- **ADR-4** `4_geocoding_and_geo_search.adoc` (Accepted 2026-05-13) - geocode addresses synchronously at write time in the `CreateVolunteerOpportunity` / `UpdateVolunteerOpportunity` handlers via an `IGeocodingService` backed by OpenStreetMap Nominatim, persisting coordinates on the `Address` value object. Reads never geocode. Radius search is a SQL bounding-box prefilter plus in-memory Haversine refinement on the small candidate set. **No PostGIS** - it was rejected to avoid a Postgres extension in every environment. Nominatim needs no API key and is throttled to roughly one request per second (`GeocodingOptions.MinRequestIntervalMilliseconds` defaults to 1100ms). Write-time unreachability persists null coordinates: the opportunity lists but has no map pin until a later update succeeds.

## Technical-debt records (docs/TDRs/)

- **TDR-1** `1_rate_limiting.adoc` (dated 2026-05-03, Status: Resolved) - the API originally shipped with no throttling. This was the open pre-1.0 debt; it is now resolved in issue #125 using `Microsoft.AspNetCore.RateLimiting`. The two policy-name constants live in `Api.Common.RateLimiting.RateLimitingPolicies`; their limits are configured in `RateLimitingExtensions` from `RateLimitingOptions` defaults: `rate-limit-read` (authenticated 200/min partitioned by `sub`, anonymous 60/min by IP) and `rate-limit-write` (100/min by `sub`, falling back to IP). `app.UseRateLimiter()` runs after `UseAuthentication()` so per-user partitioning has an identity; rejected requests get HTTP 429. If you touch rate limiting, the decision record is TDR-1, not an ADR.

## Adding a new record

New records take the next number in sequence with `{number}_{snake_case_title}.adoc`. TDRs follow `docs/TDRs/template.adoc`. ADRs start `Proposed`, become `Accepted` once agreed, and are referenced from arc42 chapter 9 (`09_architecture_decisions.adoc`); TDRs are referenced from chapter 11 (`11_technical_risks.adoc`). AsciiDoc uses tab indentation and keeps paragraphs on one unwrapped line, or CI's editorconfig job fails.

## Wiki boundary

The wiki holds informal knowledge only - traps, gotchas, and process not yet worth a formal record. Anything that is already an ADR or TDR belongs in `docs/`, linked from here, never restated. A wiki page that duplicates a decision is a maintenance liability the moment the decision changes.

# Related

- [backend-conventions](/reference/backend-conventions.md) - rate limiting is TDR-1 and the layering traces to the ADRs
- [domain-events-noop](/gotchas/domain-events-noop.md) - the DDD revival is tracked as a formal decision handoff
- [keycloak-realm-config](/reference/keycloak-realm-config.md) - ADR-3 is the decision behind self-hosted Keycloak
- [wiki-maintenance](/process/wiki-maintenance.md) - the wiki complements these formal docs and must not duplicate them

# Citations

- `docs/AGENTS.md`
- `docs/ADRs/`
- `docs/TDRs/1_rate_limiting.adoc`
