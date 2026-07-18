# Lens: Security smells

Goal: surface risks a maintainer should close - misconfigurations,
missing checks, leaky defaults. This is a smell review, not a pentest:
no exploit development, no attack payloads in the report.

## Method

1. **Secrets in tracked files:** pattern scan the whole tree (`secret`,
   `token`, `apikey`, `password`, `-----BEGIN`, JWTs, connection
   strings). Judge hits against context - keycloak realm exports and
   docker-compose dev credentials are expected dev material (repo-map
   trap 4); the finding, if any, is prod-reachability or docs failing to
   say "dev only". Check git history of any real hit (a rotated secret
   still in history is a finding).
2. **Endpoint authorization sweep:** enumerate all Api endpoints;
   for each, the auth requirement (anonymous? authenticated? policy?).
   The table itself goes in the report. Flag: mutating endpoints without
   authz, missing object-level ownership checks (pick the 2-3 most
   sensitive and trace them), admin operations gated only client-side.
3. **Input handling:** raw SQL/interpolated queries (EF mostly protects;
   search for `FromSql`, `ExecuteSql`), file/upload handling, redirect
   targets from user input, QR/check-in payload validation.
4. **Frontend token handling:** where do Keycloak tokens live
   (memory/storage?), does the API client attach them safely, any
   secrets in `import.meta.env` that ship to the bundle
   (`VITE_`-prefixed = public - check nothing sensitive is).
5. **Headers & serving:** nginx/entrypoint config under
   `frontend/docker-entrypoint.d` - CSP, frame, CORS on the API side
   (who may call it?).
6. **Supply chain & process:** what security.yml actually scans (and
   what it misses - backend? containers?), Renovate coverage,
   `pnpm audit --prod` (runnable here - run it), Dockerfile bases
   pinned/updated, workflow permissions (`permissions:` blocks -
   default token too broad? `pull_request_target` usage?).
7. **Keycloak themes:** FTL templates echoing user input unescaped.

## Verification bar

Every finding names the concrete exposure and who can trigger it.
Authorization findings quote the endpoint registration. If dev-only
context plausibly defuses a hit, say so and grade severity accordingly
- crying wolf on dev credentials erodes the report's credibility.
Sandbox cannot run the stack: dynamic behavior claims cap at Likely.
