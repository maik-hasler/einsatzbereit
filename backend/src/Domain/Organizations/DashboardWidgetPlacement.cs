namespace Domain.Organizations;

// One widget's slot in a customized dashboard: its catalog key. Position in
// the containing list is the display order - no separate Order field
// needed. Size is no longer stored here - widgets auto-fit into whatever
// space the frontend's packing algorithm gives them (#771 follow-up review
// feedback - "forget about the sizes slider").
public sealed record DashboardWidgetPlacement(DashboardWidgetKey WidgetKey);
