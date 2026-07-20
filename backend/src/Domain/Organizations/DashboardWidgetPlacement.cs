namespace Domain.Organizations;

// One widget's slot in a customized dashboard: its catalog key and the
// display size the organizer picked for it. Position in the containing list
// is the display order - no separate Order field needed.
public sealed record DashboardWidgetPlacement(
	DashboardWidgetKey WidgetKey,
	DashboardWidgetSize Size);
