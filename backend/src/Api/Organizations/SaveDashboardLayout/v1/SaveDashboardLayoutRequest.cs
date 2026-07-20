namespace Api.Organizations.SaveDashboardLayout.v1;

public sealed record DashboardWidgetPlacementRequest(string WidgetKey);

public sealed record SaveDashboardLayoutRequest(List<DashboardWidgetPlacementRequest> Widgets);
