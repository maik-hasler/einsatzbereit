# Dashboard grid v3: auto-fit sizing + green cell backdrop + adaptive widgets

## Context

Round 3 of review feedback on the org dashboard widget grid (PR #771). The
repo owner wants to drop manual sizing entirely: no more "Medium"/"Large"
slider. Instead, widgets should auto-fit into whatever space is available
in an 8-column grid, a full backdrop of green cells should show the grid
structure behind the widgets while editing (cells slightly larger than the
widget they sit under), and each widget should render a different, purpose-
built layout depending on how much room it actually gets - not just resize
its existing content.

Research already done (see prior turn): keep hand-rolling on the existing
`@dnd-kit` foundation rather than adopting `react-grid-layout` - that
library would mean discarding the tuned keyboard-drag-handle + ARIA-live +
delay-based-touch accessibility work already in place, for a library with
its own open keyboard/resize a11y gaps, and the green-cell backdrop is
bespoke rendering either way.

Confirmed via `OrganizationDashboardLayoutConfiguration.cs`: `Widgets` is
stored as a single JSON **text** column (not relational columns), so
removing the `Size` field from `DashboardWidgetPlacement` needs **no EF
Core migration** - just delete the field from the record; old rows'
now-unrecognized `"size"` JSON property is silently ignored by
`System.Text.Json` deserialization.

## Data model changes

**Backend** (no migration needed - JSON blob column):
- `Domain/Organizations/DashboardWidgetPlacement.cs`: drop `Size` entirely -
  `record DashboardWidgetPlacement(DashboardWidgetKey WidgetKey)`.
- Delete `Domain/Organizations/DashboardWidgetSize.cs` (no longer used
  anywhere).
- `Application/Organizations/GetDashboardLayout/v1/DashboardLayoutResponse.cs`:
  `DashboardWidgetPlacementResponse(string WidgetKey)` (drop `Size`).
- `Application/Organizations/SaveDashboardLayout/v1/SaveDashboardLayoutCommand.cs`
  + handler: `DashboardWidgetPlacementInput` drops `Size`; `ParseWidgets`
  drops the size-parsing branch.
- `Api/Organizations/SaveDashboardLayout/v1/SaveDashboardLayoutRequest.cs`:
  drop `Size` from the per-widget request shape.
- Update `Application.UnitTests` for both handlers to match (remove
  Size-related assertions/setup).
- `dotnet build` twice (regenerates `openapi-v1.json` then the NSwag
  clients) same as previous rounds.

**Frontend catalog** (`widgetCatalog.ts`): replace the
`WidgetSize`/`allowedSizes`/`defaultSize`/`widgetColSpanClass` model with,
per widget:
```ts
interface WidgetCatalogEntry {
  titleKey: string;
  minCols: number;   // out of 8
  maxCols: number;   // out of 8 - how wide it can grow to fill space
  rows: number;       // fixed height in row units (nominal estimate;
                       // actual CSS row uses minmax(unit, auto) so real
                       // content taller than the estimate still fits)
}
```
Values (doubling the old 4-col-based Medium/Large spans onto the new
8-col scale; Small becomes ~2-3 cols):
| Widget | minCols | maxCols | rows |
|---|---|---|---|
| CreateOpportunity | 2 | 4 | 2 |
| ToDo | 3 | 8 | 2 |
| UpcomingOpportunities | 3 | 8 | 3 |
| Calendar | 3 | 8 | 6 |
| Settings | 3 | 8 | 2 |
| QuickCheckIn | 2 | 4 | 2 |
| SettingsIcon | 2 | 2 | 1 | (fixed - min=max, always compact)

Add a shared `classifyWidth(colSpan: number): "compact" | "medium" | "full"`
helper (compact ≤3, medium 4-5, full ≥6) - passed as a `size` prop into
every widget component so each can render its own layout variant.

## Packing algorithm (replaces `widgetColSpanClass`)

A shelf/skyline packer, pure function of the (drag-ordered) widget key
list - lives in `widgetCatalog.ts` next to the other layout helpers:

```ts
export interface PackedWidget {
  widgetKey: WidgetKey;
  col: number;      // 1-based grid-column-start
  colSpan: number;
  row: number;      // 1-based grid-row-start
  rowSpan: number;
}

export function packWidgets(order: WidgetKey[]): {
  placed: PackedWidget[];
  totalRows: number;
} {
  const COLS = 8;
  let col = 0, rowStart = 1, shelfHeight = 0;
  const placed: PackedWidget[] = [];
  for (const key of order) {
    const e = WIDGET_CATALOG[key];
    if (col > 0 && e.minCols > COLS - col) {
      rowStart += shelfHeight;
      col = 0;
      shelfHeight = 0;
    }
    const span = Math.min(e.maxCols, COLS - col);
    placed.push({ widgetKey: key, col: col + 1, colSpan: span, row: rowStart, rowSpan: e.rows });
    col += span;
    shelfHeight = Math.max(shelfHeight, e.rows);
  }
  return { placed, totalRows: rowStart + shelfHeight - 1 };
}
```

Each widget greedily takes up to its `maxCols` in the current shelf (row
band); if even its `minCols` doesn't fit what's left, it wraps to a fresh
shelf. This directly gives "takes as much space as is left." Because every
catalog `minCols` is ≤ 8, a widget always fits somewhere (worst case, its
own full shelf) - there is no real "doesn't fit" failure state to design a
warning for, so **no "can't fit" hint UI is being added**; that requirement
doesn't have a triggering scenario under this algorithm (calling this out
explicitly rather than building speculative UI for it).

Drag mechanics are unchanged: `handleDragOver`/`arrayMove` still reorder
the same `WidgetPlacement[]`-shaped list (now just `{ widgetKey }[]`), and
`packWidgets` is re-run on every render from that order - same integration
point that already exists today for the col-span lookup, so no changes to
`DndContext`/sensors/`DragOverlay`/the hidden keyboard grip button/ARIA.

## Rendering changes (`index.tsx`)

- Container: `grid grid-cols-1 lg:grid-cols-8 gap-4 lg:auto-rows-[minmax(64px,auto)]`
  (mobile still collapses to a single column, unchanged from today).
- Each `EditableWidgetTile` gets its placement via inline style now (span
  is dynamic, not a fixed Tailwind class):
  `style={{ gridColumn: \`span ${colSpan}\`, gridRow: \`span ${rowSpan}\` }}`
  at the `lg` breakpoint only (mobile stays single-column/stacked, so no
  grid-column/row style needed there - apply via a wrapping media check or
  just let it apply unconditionally since `grid-column: span N` on a
  single-column grid is harmless).
- Green cell backdrop: rendered as extra grid items **in the same grid
  container** as the real widgets (not a separate overlay grid) so row
  tracks are guaranteed to line up even when a widget's content grows
  taller than its nominal `rows` estimate. For every cell in the
  `8 x (totalRows + 1)` space (the "+1" row is the buffer the user asked
  for), render a `pointer-events-none` filler div placed at that exact
  `gridColumn`/`gridRow`, styled `bg-green-300/40` with a small negative
  margin (`-m-1`) so it visually bleeds slightly past the real widget's
  edges into the gap ("expands a bit over the actual widget"). Real widget
  tiles render after the fillers in DOM order so they paint on top
  wherever they overlap. Only shown while `editing`.
- Widget opacity while editing: tune from the current `opacity-75` down to
  `opacity-60` so the green shows through more clearly (still using the
  existing `inert` + opacity wrapper - unchanged mechanism, just the
  value).
- Remove entirely: the old 3-line column-guide overlay (superseded by the
  full cell backdrop), the resize `<input type="range">` and its
  wrapping div, `onResize`/`handleResizeWidget`, `sizeIndex`,
  `widgetColSpanClass` import.
- `renderWidget(key)` now also passes `size={classifyWidth(colSpan)}` to
  each widget component (colSpan comes from that widget's `PackedWidget`
  entry) alongside its existing props.

## Per-widget adaptive layouts

Each component gets a new `size: "compact" | "medium" | "full"` prop and
branches its JSX. Keep this additive/simple - reuse existing sub-elements,
just rearranged or trimmed, not new visual designs from scratch:

- **ToDoWidget**: `compact` stacks the two stats vertically (`grid-cols-1`
  instead of `grid-cols-2`); `medium`/`full` keep today's side-by-side
  `grid-cols-2`.
- **UpcomingOpportunitiesWidget**: `compact` shows top 2 items, title only
  (no date/participants line); `medium` shows today's up-to-5 with full
  metadata; `full` unchanged (same list, just more breathing room).
- **CalendarWidget**: `compact` defaults to react-big-calendar's `agenda`
  view (a simple upcoming-events list - the library already supports this
  view, just add it to the `views` array and set as initial `calView` when
  `size === "compact"`) instead of the month grid; `medium` defaults to
  `week`; `full` keeps today's `month` default. All three views remain
  switchable via the existing view buttons regardless of size - only the
  *default* view changes.
- **SettingsWidget**: `compact` stacks the logo above the text block
  instead of the row layout, drops the "created on" date; `medium`/`full`
  unchanged.
- **CreateOpportunityWidget**: `compact` drops the description paragraph,
  button becomes icon+short label; `medium`/`full` unchanged (description +
  full button).
- **QuickCheckInWidget**: `compact`/`medium` keep today's stacked
  select-then-button; `full` puts them side by side in a flex row.
- **SettingsIconWidget**: no change - `minCols === maxCols` so it only ever
  renders `compact`, which is what it already looks like.

## Locale keys

Remove now-dead keys from both `en.json` and `de.json`:
`widgetSizeSmall`, `widgetSizeMedium`, `widgetSizeLarge`, `changeWidgetSize`.
`dragToReorder` stays (still the keyboard grip handle's label).

## Tests

- `backend/tests/Application.UnitTests/.../GetDashboardLayoutQueryHandlerTests.cs`
  and `SaveDashboardLayoutCommandHandlerTests.cs`: drop Size from
  construction/assertions.
- `backend/tests/VisualTests/OrgDashboardCustomizeTests.cs`: delete
  `ResizingAWidgetViaSlider_AndSaving_PersistsAcrossReload` (the slider it
  tests no longer exists); other tests (add/remove/reorder/empty-state)
  need no changes since the underlying testids (`widget-tile-*`,
  `dashboard-widget-grid`, `dashboard-empty-state`, remove buttons)
  survive unchanged.
- `scripts/smoke-test-771-dashboard-drag-and-resize.mjs`: update - remove
  the slider assertions (replace with an assertion that a dragged widget's
  computed `gridColumn`/`gridRow` actually changes after a reorder), keep
  the live-reflow, empty-state, and touch-action checks as-is since those
  are unaffected by this round.

## Verification

1. `dotnet build` (twice) + `Application.UnitTests` + `ArchitectureTests`
   locally (no Docker needed, per this repo's sandbox notes).
2. `pnpm check` / `pnpm lint` / `pnpm format:write` / `pnpm build` in
   `frontend/`.
3. Self-review the diff (nswag-check for the DTO changes, i18n-check for
   the removed locale keys, a11y-check for the widget-size-variant markup
   changes - no new drag/keyboard mechanics, but new conditional JSX per
   widget is worth a pass).
4. Live-verify on staging the same way as the last two rounds (already-
   authenticated `vera` test account, `scripts/lib` pattern): confirm the
   green backdrop renders, widgets visibly grow/shrink to fill a
   reordered row, and each widget's compact/medium/full variant actually
   shows the intended different layout at its resulting size - update
   `scripts/smoke-test-771-dashboard-drag-and-resize.mjs` accordingly and
   re-run it against the deployed RC.
5. Cut the next RC, deploy, document results as a PR comment (the user
   will post it themselves - no GitHub write access from this session).
