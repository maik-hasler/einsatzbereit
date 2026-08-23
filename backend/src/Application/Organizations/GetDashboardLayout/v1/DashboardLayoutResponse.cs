namespace Application.Organizations.GetDashboardLayout.v1;

public sealed record DashboardWidgetPlacementResponse(string WidgetKey, int X, int Y, int Width, int Height);

public sealed record DashboardLayoutResponse(bool HasCustomLayout, List<DashboardWidgetPlacementResponse> Widgets);
