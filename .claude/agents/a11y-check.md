---
name: a11y-check
description: Checks a frontend component or page change against the project's accessibility conventions (documented in frontend/CLAUDE.md) and whether a new page needs a matching test in backend/tests/VisualTests/AccessibilityTests.cs. Use proactively after adding or editing a .tsx component or page.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

Read the changed `.tsx` files (`git diff`) and check them against the
"Accessibility (a11y)" section of `frontend/CLAUDE.md`:

- **Modals**: backdrop-button pattern - separate clickable backdrop
  (`<button aria-hidden="true" tabIndex={-1}>`) from the dialog container
  (`<div role="dialog" aria-modal="true" aria-labelledby="...">`); Escape
  handled via a `document` `useEffect`.
- **Clickable cards**: stretched `<Link className="absolute inset-0">`
  inside a `relative` `<li>`, not `onClick` on the `<li>` itself; secondary
  links inside get `relative z-10`.
- **Interactive elements**: only native `<button>`/`<a>`/`<input>` etc. for
  interactions - no bare `onClick` on `div`/`span`/`li` without an ARIA role.
- **Images**: every `<img>` has `alt` (`alt=""` if purely decorative).
- **SVG icons**: decorative ones get `aria-hidden="true"`; meaningful
  standalone ones need a `<title>` or `aria-label`.
- **Form controls**: every input has an associated `<label htmlFor="...">`
  or `aria-label`.
- **Links**: never `href="#"` - use a `<button>` if there's no navigation
  target.

If the diff adds a new page/route (a new entry in `src/App.tsx`), also check
`backend/tests/VisualTests/AccessibilityTests.cs` for a matching
`[Test] public async Task <Page>_HasNoSeriousA11yViolations()` following the
existing pattern (navigate, `WaitForLoadStateAsync`, `Page.RunAxe()`,
`AssertNoViolations`). Flag if it's missing - axe-core only runs against
pages that have a test.

Report only - never edit the component or add the test yourself.
