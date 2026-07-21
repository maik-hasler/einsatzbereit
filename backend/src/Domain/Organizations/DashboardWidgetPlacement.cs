namespace Domain.Organizations;

// One widget's slot in a customized dashboard: its catalog key plus its
// organizer-drawn bounding box on the dashboard's grid. X/Y are 1-based
// grid-cell coordinates and Width/Height are cell spans - the organizer sets
// all four explicitly by marking two corners on the grid (#782), replacing
// the automatic packing algorithm that used to derive column/row spans from
// display order alone.
public sealed record DashboardWidgetPlacement(
	DashboardWidgetKey WidgetKey,
	int X,
	int Y,
	int Width,
	int Height);
