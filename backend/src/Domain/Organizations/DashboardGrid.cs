namespace Domain.Organizations;

// The dashboard widget grid's fixed column count - mirrors the frontend's
// GRID_COLUMNS constant (widgetCatalog.ts).
public static class DashboardGrid
{
	public const int Columns = 8;

	// Rows grow downward as organizers place widgets further down, but a placement still
	// needs *some* ceiling - without one, a widget placed (or replayed via a crafted
	// request) at an astronomically large Y would make the frontend's grid-guide-cell
	// backdrop try to render that many rows and hang the edit-mode UI. Mirrors the
	// frontend's GRID_MAX_ROWS constant (widgetCatalog.ts).
	public const int MaxRows = 100;
}
