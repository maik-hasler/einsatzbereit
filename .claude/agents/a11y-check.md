---
name: a11y-check
description: Checks a frontend component or page change against the project's a11y conventions that ESLint can't check (documented in frontend/AGENTS.md), and whether a new page needs a matching test in backend/tests/VisualTests/AccessibilityTests.cs. Use proactively after adding or editing a .tsx component or page.
tools: Bash, Read, Grep, Glob
disallowedTools: Write, Edit
---

Scope is deliberately narrow: `eslint-plugin-jsx-a11y` (recommended ruleset,
CI-blocking) already deterministically catches missing `alt`, unlabelled form
controls, and bare `onClick` on non-interactive elements - don't re-check
those, ESLint already guarantees them. Only check what nothing else catches:

Read the changed `.tsx` files (`git diff`) and check them against the
project-specific patterns in the "Accessibility (a11y)" section of
`frontend/AGENTS.md` that aren't generic a11y rules:

- **Modals**: backdrop-button pattern - separate clickable backdrop
  (`<button aria-hidden="true" tabIndex={-1}>`) from the dialog container
  (`<div role="dialog" aria-modal="true" aria-labelledby="...">`); Escape
  handled via a `document` `useEffect`.
- **Clickable cards**: stretched `<Link className="absolute inset-0">`
  inside a `relative` `<li>`, not `onClick` on the `<li>` itself; secondary
  links inside get `relative z-10`.
- **SVG icons**: decorative ones get `aria-hidden="true"`; meaningful
  standalone ones need a `<title>` or `aria-label` (jsx-a11y has no rule for
  this - `alt-text` only covers `<img>`).
- **Links**: never `href="#"` - use a `<button>` if there's no navigation
  target (not caught by any jsx-a11y rule).

If the diff adds a new page/route (a new entry in `src/App.tsx`), also check
`backend/tests/VisualTests/AccessibilityTests.cs` for a matching
`[Test] public async Task <Page>_HasNoSeriousA11yViolations()` following the
existing pattern (navigate, `WaitForLoadStateAsync`, `Page.RunAxe()`,
`AssertNoViolations`). Flag if it's missing - a page with no test gets zero
axe-core coverage, silently, forever.

Report only - never edit the component or add the test yourself.
