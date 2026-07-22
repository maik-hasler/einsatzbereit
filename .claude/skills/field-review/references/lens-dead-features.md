# Lens: Dead features

Goal: features that are implemented but unreachable, unused, or
abandoned - reachable-code cousins of dead code. The unit of finding is
a *capability*, not a symbol.

## Method

1. **Route reachability (frontend):** extract the route table from
   `App.tsx` / router setup. For each route: is it linked from any
   navigation, page, or redirect? Orphan routes = candidate. Check
   deep-link legitimacy (a QR/scan flow may be entered externally by
   design - the check-in flow likely is).
2. **Endpoint → UI reachability:** list backend endpoints (Api folder
   structure makes this mechanical). Map each to generated-client
   methods, then to call sites in `src/`. Client methods with zero call
   sites = feature exists server-side, no UI triggers it. Report as
   dead-from-UI; note the API is public, so "delete" vs "document as
   API-only" is the user's call.
3. **Write-only data:** entity properties that are set but never read in
   any handler, projection, or frontend display. Trace both directions.
4. **Toggles and config:** feature flags, config switches, or env vars
   that are read but constant in every environment (`.env.example`,
   docker-compose, AppHost, workflow env) - the disabled arm is a dead
   feature.
5. **Half-features:** UI elements that render but lead nowhere (buttons
   without handlers, modals never opened - search each modal component
   for its open-trigger), i18n strings for flows that don't exist.
6. **Keycloak surface:** realm capabilities (flows, clients, roles) and
   theme templates not exercised by the app's auth setup.

## Verification bar

Reachability claims are graph claims - show the traversal: "route X
appears in App.tsx:NN; searched `to=`/`navigate(`/`href` across src/,
zero references." For endpoint findings, name the client method and the
zero-call-site search. Anything relying on "probably nobody uses this"
is a Hypothesis.

## Traps

Admin-only surfaces (`AdministrationPage.tsx` exists) can look
orphaned from the normal nav - check role-gated navigation before
flagging. External entry points: QR codes, emails (Notifications
feature!), and Keycloak redirects reach routes without in-app links.
Search email/notification templates for URLs before declaring a route
orphaned.
