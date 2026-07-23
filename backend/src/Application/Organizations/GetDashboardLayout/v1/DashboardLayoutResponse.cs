namespace Application.Organizations.GetDashboardLayout.v1;

// WidgetKey travels as a string (not the Domain enum) to keep the wire
// contract stable regardless of enum member renumbering - same convention as
// AchievementSummary.Type. X/Y/Width/Height are the organizer-drawn grid
// position/size (#782) - 1-based grid-cell coordinates and cell spans.
public sealed record DashboardWidgetPlacementResponse(string WidgetKey, int X, int Y, int Width, int Height);

// Widgets is empty both when the organizer has never customized their
// dashboard (no OrganizationDashboardLayout row) AND when they deliberately
// removed every widget and saved that. HasCustomLayout is what tells those
// two cases apart: false means "no row yet" (frontend applies its own
// default layout), true means "a row exists" (frontend must respect an
// empty Widgets list as-is, not silently reapply the default - see #771
// review feedback, the "remove all widgets resets on refresh" bug).
public sealed record DashboardLayoutResponse(bool HasCustomLayout, List<DashboardWidgetPlacementResponse> Widgets);
