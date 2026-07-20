namespace Application.Organizations.GetDashboardLayout.v1;

// WidgetKey/Size travel as strings (not the Domain enums) to keep the wire
// contract stable regardless of enum member renumbering - same convention as
// AchievementSummary.Type.
public sealed record DashboardWidgetPlacementResponse(string WidgetKey, string Size);

// Widgets is empty when the organizer has not customized their dashboard yet
// (no OrganizationDashboardLayout row) - the frontend applies its own default
// layout in that case rather than the backend needing to know it.
public sealed record DashboardLayoutResponse(List<DashboardWidgetPlacementResponse> Widgets);
