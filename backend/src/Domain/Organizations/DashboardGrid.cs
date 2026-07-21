namespace Domain.Organizations;

// The dashboard widget grid's fixed column count - mirrors the frontend's
// GRID_COLUMNS constant (widgetCatalog.ts). Rows are unbounded; the grid
// simply grows downward as organizers place widgets further down.
public static class DashboardGrid
{
	public const int Columns = 8;
}
