using Domain.Primitives;
using Domain.Users;

namespace Domain.Organizations;

public sealed class OrganizationDashboardLayout
	: AggregateRoot<OrganizationDashboardLayoutId>,
		IAuditableEntity
{
	public OrganizationId OrganizationId { get; private set; }

	public UserId UserId { get; private set; }

	public IReadOnlyList<DashboardWidgetPlacement> Widgets { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private OrganizationDashboardLayout() : base(default) { }
#pragma warning restore CS8618

	private OrganizationDashboardLayout(
		OrganizationDashboardLayoutId id,
		OrganizationId organizationId,
		UserId userId,
		IReadOnlyList<DashboardWidgetPlacement> widgets)
		: base(id)
	{
		OrganizationId = organizationId;
		UserId = userId;
		Widgets = widgets;
	}

	public static OrganizationDashboardLayout Create(
		OrganizationId organizationId,
		UserId userId,
		IReadOnlyList<DashboardWidgetPlacement> widgets) =>
		new(
			OrganizationDashboardLayoutId.New(),
			organizationId,
			userId,
			widgets);

	public void ReplaceWidgets(IReadOnlyList<DashboardWidgetPlacement> widgets) =>
		Widgets = widgets;
}
