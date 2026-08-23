namespace Domain.Organizations;

public sealed record DashboardWidgetPlacement(
	DashboardWidgetKey WidgetKey,
	int X,
	int Y,
	int Width,
	int Height);
