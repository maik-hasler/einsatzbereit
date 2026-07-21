namespace Api.Organizations.SaveDashboardLayout.v1;

public sealed record DashboardWidgetPlacementRequest(string WidgetKey, int X, int Y, int Width, int Height);

public sealed record SaveDashboardLayoutRequest(List<DashboardWidgetPlacementRequest> Widgets);
