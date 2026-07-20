namespace Api.Organizations.SaveDashboardLayout.v1;

public sealed record DashboardWidgetPlacementRequest(string WidgetKey, string Size);

public sealed record SaveDashboardLayoutRequest(List<DashboardWidgetPlacementRequest> Widgets);
