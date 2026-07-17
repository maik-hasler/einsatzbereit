using Domain.Primitives;

namespace Domain.Organizations;

public readonly record struct OrganizationMembershipId : IValueObject
{
	public Guid Value { get; }

	private OrganizationMembershipId(Guid value) => Value = value;

	public static Result<OrganizationMembershipId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<OrganizationMembershipId>(Error.Validation("OrganizationMembershipId.Empty", "OrganizationMembershipId must not be empty."))
			: Result.Success(new OrganizationMembershipId(value));

	public static OrganizationMembershipId New() => new(Guid.CreateVersion7());
}
