using Domain.Primitives;

namespace Domain.Organizations;

public readonly record struct OrganizationInvitationId : IValueObject
{
	public Guid Value { get; }

	private OrganizationInvitationId(Guid value) => Value = value;

	public static Result<OrganizationInvitationId> Create(Guid value) =>
		value == Guid.Empty
			? Result.Failure<OrganizationInvitationId>(Error.Validation("OrganizationInvitationId.Empty", "OrganizationInvitationId must not be empty."))
			: Result.Success(new OrganizationInvitationId(value));

	public static OrganizationInvitationId New() => new(Guid.CreateVersion7());
}
