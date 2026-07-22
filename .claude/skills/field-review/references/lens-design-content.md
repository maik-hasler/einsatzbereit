# Lens: Design & content

Goal: pages and components that are ugly, inconsistent, or visibly thinner
than the product around them - not a subjective taste pass, an evidenced one
backed by screenshots and, where possible, a root cause in source.

## Method

1. **Screenshot every major page/tab across personas and at least one
   mobile viewport** - reuse `lens-personas.md`'s driving infrastructure if
   that lens ran in the same session; don't re-derive it. Full-page
   screenshots, not viewport-cropped, so density is visible at a glance.
2. **Compare siblings, not pages in isolation.** Tabs in the same shell
   (an org app's Opportunities vs. Members vs. Settings), or pages serving
   a comparable role (a public org profile vs. a public user profile) are
   the highest-signal comparison - a real product usually has one visual
   language, and a tab that's visibly plainer or emptier than its
   neighbors is a finding even if it "works" fine on its own.
3. **Use `body.innerText` length as a sparse-content signal**, same
   technique as the personas lens: a detail/profile/dashboard page with a
   fraction of the text content of comparable pages is worth a screenshot
   and a look, not an assumption that it's fine.
4. **When you spot a layout anomaly** (a dead column, unbalanced
   whitespace, content pushed to one side under a full-width heading),
   don't just describe the pixels - grep the component/page for the class
   or wrapper pattern responsible (`className="mx-auto max-w-2xl"` was a
   real repo example: one wrapper, reused in four files, each looking like
   an unrelated "sparse page" until the grep tied them together). Report
   the shared root cause and every file it appears in, not each symptom
   separately.
5. **Check for a visual-language split.** Do "discovery" surfaces (browse,
   list, landing) and "transactional" surfaces (forms, modals, admin
   tables) look like the same product? A repo-wide plain-form convention
   is a legitimate deliberate choice (check `lib/formClasses.ts` or
   equivalent before assuming it's an oversight) - the finding, if there is
   one, is the *contrast* being jarring next to a much more polished
   neighbor, not the plainness itself.
6. **Data hygiene is a content finding here too.** A list or profile
   dominated by smoke-test debris (`Smoke...`-prefixed titles, a bio field
   reading like a test fixture) makes the product look broken to anyone
   who looks, even though it's a data problem, not a code problem. Report
   it, but label it as data hygiene (see `lens-personas.md`'s Traps) and
   point at `.github/workflows/reset-staging.yml` rather than proposing a
   code fix for it.
7. **Give credit where it's due.** A findings-only report reads as more
   broken than the product is. Note 3-5 things that are genuinely well
   built (a rich filter bar, a well-illustrated 404 page, a cohesive
   badge/achievement system) alongside the problems - it costs one line
   each and makes the rest of the report more trustworthy, not less.

## Verification bar

A design finding names the exact page, includes what was observed (not
just "looks bad" - describe the specific imbalance, inconsistency, or
gap), and where the cause is structural (a shared component/wrapper),
cites every file it touches. A "this feels sparse" claim without a
side-by-side comparison to a sibling page is a Hypothesis.

## Traps

Don't confuse "different because it's a different kind of screen" with
"inconsistent" - a data table and a marketing hero are allowed to look
different. The finding is when two screens playing the *same role*
(two profile pages, two tabs in the same shell) don't. Screenshot timing
artifacts (a page caught mid-load) look identical to genuine sparseness in
a still image - cross-check with the `innerText`-length signal and, if
still unsure, a longer wait before reporting.
