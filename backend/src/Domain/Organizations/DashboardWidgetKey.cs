namespace Domain.Organizations;

public enum DashboardWidgetKey
{
	ToDo,
	UpcomingOpportunities,
	Calendar,
	Settings,
	CreateOpportunity,
	QuickCheckIn,
	SettingsIcon,
	// Appended rather than slotted next to ToDo (#1780): placements are
	// persisted as the member NAME (see OrganizationDashboardLayoutConfiguration's
	// JsonStringEnumConverter), so ordering carries no stored meaning - but
	// appending keeps every existing member's numeric value untouched.
	VolunteerStats,
}
