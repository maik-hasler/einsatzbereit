using Domain.Primitives;

namespace Domain.Organizations;

public readonly record struct OrganizationDashboardLayoutId : IValueObject
{
	public Guid Value { get; }

	private OrganizationDashboardLayoutId(Guid value) => Value = value;

	public static Result<OrganizationDashboardLayoutId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<OrganizationDashboardLayoutId>(Error.Validation("OrganizationDashboardLayoutId.Empty", "OrganizationDashboardLayoutId must not be empty."))
			: Result.Success(new OrganizationDashboardLayoutId(value));

	public static OrganizationDashboardLayoutId New() => new(Guid.CreateVersion7());
}
