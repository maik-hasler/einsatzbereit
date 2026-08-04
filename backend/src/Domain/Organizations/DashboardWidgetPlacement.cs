namespace Domain.Organizations;

// One widget's slot in a customized dashboard: its catalog key plus its
// organizer-drawn bounding box on the dashboard's grid. X/Y are 1-based
// grid-cell coordinates and Width/Height are cell spans, set explicitly by
// the organizer marking two corners on the grid (#782).
public sealed record DashboardWidgetPlacement(
	DashboardWidgetKey WidgetKey,
	int X,
	int Y,
	int Width,
	int Height);
