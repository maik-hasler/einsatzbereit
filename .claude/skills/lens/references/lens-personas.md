# Lens: Personas

Goal: friction, breakage, visual inconsistency, and content gaps a real
user hits - not from reading code, from actually being Volunteer Vera,
Organizer Olaf, or Platform Admin on live staging. This is the one lens
that runs the app instead of reading it; it catches what static lenses
structurally cannot - a page that renders without error but reads as
empty, a control that "works" but confuses, a tab that's visibly plainer
than its neighbors.

Functional breakage and visual/content quality used to be two separate
lenses here. They were merged: both come from the same browser session,
the same screenshots, the same signals - splitting them meant describing
the same page twice under two different framings instead of once.

## Method - drive it yourself, in this session

Live staging only (root `AGENTS.md`, "Sandbox Limitations" - no local
Aspire/Docker stack here). Run this yourself in the current session, never
delegate it to a subagent: MCP tool grants, including the `playwright`
plugin, do not propagate to a spawned subagent, so it would silently have no
way to browse (confirmed the hard way - see root `AGENTS.md`). `ToolSearch`
for `browser_navigate` first; if nothing resolves, fall back to the
`/live-verify` skill's scratch-script recipe.

1. **One browser context per persona, never reused.** Signing out and back
   in inside one context is fragile (session state bleeds, a failed
   sign-out silently leaves you testing the wrong persona under the wrong
   label) - open a fresh context per persona instead.
2. **Walk each persona's realistic flows end to end**, not a single screen.
   Anonymous visitor, Volunteer Vera (`vera/vera123`), Organizer Olaf
   (`olaf/olaf123`), Platform Admin (`admin/admin123`) - see
   `wiki/bundle/project/pre-launch-testing-event.md` for the role-card
   framing these personas come from. Component/page names drift fast in
   this repo; trust `frontend/src/App.tsx`'s routes over any name written
   down here or in a previous report.
3. **Capture more than screenshots at every meaningful state:**
   full-page screenshot, `body.innerText` length (a cheap, surprisingly
   reliable sparse-content signal - compare it across pages of similar
   importance), console errors, and every network response with status
   >= 400. The 400+ capture is what actually catches the sharpest bugs -
   a page can look fine while its underlying API calls are failing
   silently underneath it.
4. **Don't trust a "stuck" first impression.** A page that shows a bare
   "Loading..." or looks blank on first screenshot may just be slower than
   your script's wait condition (`networkidle` resolves before a
   post-redirect effect fires its own fetch). Re-navigate with an explicit
   extra wait (5-10s) and re-check before reporting something as broken -
   otherwise you'll report a script-timing artifact as a product bug (the
   same caution applies to a still image catching a page mid-load: it can
   look sparse without being sparse).
5. **A mobile pass on anything you flag.** 390x844 at minimum. Something
   that's a rough edge on desktop can be a dead end - or fine - on mobile;
   don't assume either way without checking.
6. **Compare siblings, not pages in isolation**, for visual/content
   findings specifically. Tabs in the same shell (an org app's
   Opportunities vs. Members vs. Settings), or pages serving a comparable
   role (a public org profile vs. a public user profile), are the
   highest-signal comparison - a real product usually has one visual
   language, and a tab that's visibly plainer or emptier than its
   neighbors is a finding even if it "works" fine on its own. Don't
   confuse "different because it's a different kind of screen" (a data
   table vs. a marketing hero, allowed to differ) with "inconsistent"
   (two screens playing the *same role* that don't match).
7. **Check for a visual-language split.** Do "discovery" surfaces
   (browse, list, landing) and "transactional" surfaces (forms, modals,
   admin tables) look like the same product? A repo-wide plain-form
   convention can be a legitimate deliberate choice (check
   `lib/formClasses.ts` or equivalent before assuming it's an oversight) -
   the finding, if there is one, is the *contrast* being jarring next to a
   much more polished neighbor, not the plainness itself.
8. **When a live observation looks systemic, root-cause it with a grep
   before writing it up.** A single visual anomaly (a blank column, a
   misaligned block) is worth one line; the same grep-able pattern
   appearing in three files is worth naming the shared root cause and
   citing all three - much more actionable, and it's usually one fix
   instead of three. (`className="mx-auto max-w-2xl"` was a real repo
   example: one wrapper, reused in four files, each looking like an
   unrelated "sparse page" until the grep tied them together.)
9. **Give credit where it's due.** A findings-only report reads as more
   broken than the product is. Note 3-5 things that are genuinely well
   built (a rich filter bar, a well-illustrated 404 page, a cohesive
   badge/achievement system) alongside the problems - it costs one line
   each and makes the rest of the report more trustworthy, not less.

## What counts as a finding

- Breaks outright: error, dead end, silent no-op, a network call failing
  (400+) with no visible error surfaced to the user.
- Would confuse someone doing the realistic task: unclear state, missing
  feedback, a control that doesn't do what it looks like it does.
- Falls short of what the persona's role actually needs to get the
  real-life job done - "Olaf can't tell if anyone applied without opening
  every opportunity", not "wouldn't it be nice if...".
- Looks visibly thinner, less polished, or inconsistent next to a sibling
  page or tab playing the same role - not a subjective taste call, one
  backed by the side-by-side comparison and, where the cause is
  structural, the shared component/wrapper.

## Verification bar

A finding names the exact page/URL, the persona, and the observed evidence
(screenshot description, network log line, console error, or a
side-by-side sibling comparison) - not a vague "felt off" or "looks bad".
A claim that something is *broken* (not just unpolished) needs the
retry-with-wait from step 4 first, or it caps at Likely. A "this feels
sparse" claim without a side-by-side comparison to a sibling page is a
Hypothesis. Where a design finding's cause is structural, cite every file
the shared wrapper/component appears in, not just the one you saw first.

## Traps

Staging is a shared environment reused by every deploy-verify smoke test in
`scripts/` - a cluttered opportunity list or a demo account's profile full
of `Smoke...`-prefixed junk is a data-hygiene finding (real, worth one
line, `.github/workflows/reset-staging.yml` is the fix), not evidence the
product itself is broken or badly designed. Keep the two apart. If a flow
requires creating real data to see the next screen, clean it up before the
run ends - this lens reports, it does not leave debris behind.
