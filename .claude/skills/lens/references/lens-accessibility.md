# Lens: Accessibility

Goal: accessibility problems that survive past `eslint-plugin-jsx-a11y`
(CI-blocking on every PR) and the two axe-core gates - component-level
`frontend/src/**/*.a11y.test.tsx` and page-level
`backend/tests/VisualTests/AccessibilityTests.cs` (#2148). All three are
real, all three are narrower than they sound - this lens is the depth pass
behind them, not a repeat of any.

## Method

1. **Map coverage before hunting bugs**, at both altitudes. Grep
   `AccessibilityTests.cs` for `HasNoSeriousA11yViolations` and diff that
   list against every route in `frontend/src/App.tsx`; then list
   `frontend/src/**/*.a11y.test.tsx` and diff *that* against
   `frontend/src/components/`. A route with no page scan has zero coverage
   of landmark structure, heading order and colour contrast - none of which
   jsdom can evaluate - and a component with no suite has zero coverage of
   roles and names. Either gap is itself a finding (feed it to
   `lens-test-gaps.md`'s inventory if that lens runs too, or report it
   here).
2. **Static pass for the patterns neither tool catches** (see
   `frontend/AGENTS.md`'s "Accessibility (a11y)" section for the full
   list; jsx-a11y has no rule for these): the modal backdrop-button
   pattern, meaningful standalone SVGs missing a `<title>`/`aria-label`,
   `href="#"` used where a `<button>` belongs, focus not visibly returned
   after a modal closes, a stretched-link card whose secondary links
   aren't lifted above it with `z-10`.
3. **Driven pass, where a browser is available - drive it, don't just
   read it.** Tab through a form or modal with the keyboard only: does
   focus order match visual order, is the focused element ever invisible,
   does Escape close what it should, does a skip-to-content path exist on
   pages with a lot of nav chrome. Where the `playwright` plugin is driving
   the page anyway, inject `axe-core` and run it against a page/state the
   C# suite doesn't cover (a specific modal, an edit-mode view, a widget
   dialog) as a spot-check - treat a hit here as Confirmed, same bar as
   the CI suite.
4. **Color contrast on rendered output**, not just Tailwind class names -
   a class can look fine and still fail contrast depending on what it's
   layered on. Check text-on-image (banner overlays), disabled-state
   text, and placeholder text specifically; these are the categories that
   slip past a quick visual scan.
5. **Screen-reader-shaped read of one non-trivial flow** (a multi-step
   wizard, a drag-and-resize widget dashboard): would the sequence of
   headings, labels, and live-region announcements make sense read aloud,
   in order, with no visual context? Widget/drag-and-drop UIs are the
   likeliest place for this to break silently, since the visual result can
   look correct while the accessible name/role/state trio is wrong or
   missing.

## Verification bar

A finding names the exact component/page, the specific pattern violated,
and how it was checked (static read, keyboard drive, injected axe-core, or
a rendered-contrast check) - state which, since the confidence bar differs.
A claim that "a screen reader user would struggle" without having actually
driven the flow by keyboard/axe-core caps at Hypothesis.

## Traps

Don't re-report what jsx-a11y or either existing axe suite
(`*.a11y.test.tsx`, `AccessibilityTests.cs`) already guarantees - confirm the specific rule/test doesn't cover
the case before writing it up, not just that this particular page's test
happens to pass (it may pass because the violation is in a state the test
never reaches - edit mode, a modal, an error state). A component library
default (native `<select>` styling, a browser's own focus ring) is not a
finding just because it looks plain.
