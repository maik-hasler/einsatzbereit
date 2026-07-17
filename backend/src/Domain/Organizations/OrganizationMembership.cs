using Domain.Primitives;
using Domain.Users;

namespace Domain.Organizations;

public sealed class OrganizationMembership
	: AggregateRoot<OrganizationMembershipId>,
		IAuditableEntity
{
	public OrganizationId OrganizationId { get; private set; }

	public UserId UserId { get; private set; }

	public OrganizationMemberRole Role { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private OrganizationMembership() : base(default) { }
#pragma warning restore CS8618

	private OrganizationMembership(
		OrganizationMembershipId id,
		OrganizationId organizationId,
		UserId userId,
		OrganizationMemberRole role)
		: base(id)
	{
		OrganizationId = organizationId;
		UserId = userId;
		Role = role;
	}

	public static OrganizationMembership Create(
		OrganizationId organizationId,
		UserId userId,
		OrganizationMemberRole role) =>
		new(
			OrganizationMembershipId.New(),
			organizationId,
			userId,
			role);
}
