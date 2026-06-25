using Domain.Primitives;
using Domain.Users;

namespace Domain.Organizations;

public sealed class OrganizationInvitation
	: AggregateRoot<OrganizationInvitationId>,
		IAuditableEntity
{
	public OrganizationId OrganizationId { get; private set; }

	public string OrganizationName { get; private set; }

	public UserId InviteeId { get; private set; }

	public string InviteeName { get; private set; }

	public UserId InvitedById { get; private set; }

	public InvitationStatus Status { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

#pragma warning disable CS8618
	private OrganizationInvitation() : base(default) { }
#pragma warning restore CS8618

	private OrganizationInvitation(
		OrganizationInvitationId id,
		OrganizationId organizationId,
		string organizationName,
		UserId inviteeId,
		string inviteeName,
		UserId invitedById)
		: base(id)
	{
		OrganizationId = organizationId;
		OrganizationName = organizationName;
		InviteeId = inviteeId;
		InviteeName = inviteeName;
		InvitedById = invitedById;
		Status = InvitationStatus.Pending;
	}

	public static OrganizationInvitation Create(
		OrganizationId organizationId,
		string organizationName,
		UserId inviteeId,
		string inviteeName,
		UserId invitedById) =>
		new(
			new OrganizationInvitationId(Guid.CreateVersion7()),
			organizationId,
			organizationName,
			inviteeId,
			inviteeName,
			invitedById);

	public void Accept()
	{
		if (Status != InvitationStatus.Pending)
			throw new DomainException("Invitation is not pending.");
		Status = InvitationStatus.Accepted;
	}

	public void Decline()
	{
		if (Status != InvitationStatus.Pending)
			throw new DomainException("Invitation is not pending.");
		Status = InvitationStatus.Declined;
	}
}
