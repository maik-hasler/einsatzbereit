using Domain.Primitives;
using Domain.Users;

namespace Domain.Organizations;

public sealed class OrganizationInvitation
	: AggregateRoot<OrganizationInvitationId>,
		IAuditableEntity
{
	public OrganizationId OrganizationId { get; private set; }

	public UserId InviteeId { get; private set; }

	public UserId InvitedById { get; private set; }

	public OrganizationMemberRole IntendedRole { get; private set; }

	public InvitationStatus Status { get; private set; }

	public DateTimeOffset ExpiresOn { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	public const int ExpiryWindowDays = 14;

#pragma warning disable CS8618
	private OrganizationInvitation() : base(default) { }
#pragma warning restore CS8618

	private OrganizationInvitation(
		OrganizationInvitationId id,
		OrganizationId organizationId,
		UserId inviteeId,
		UserId invitedById,
		OrganizationMemberRole intendedRole,
		DateTimeOffset now)
		: base(id)
	{
		OrganizationId = organizationId;
		InviteeId = inviteeId;
		InvitedById = invitedById;
		IntendedRole = intendedRole;
		Status = InvitationStatus.Pending;
		ExpiresOn = now.AddDays(ExpiryWindowDays);
	}

	public static OrganizationInvitation Create(
		OrganizationId organizationId,
		UserId inviteeId,
		UserId invitedById,
		OrganizationMemberRole intendedRole,
		DateTimeOffset now) =>
		new(
			OrganizationInvitationId.New(),
			organizationId,
			inviteeId,
			invitedById,
			intendedRole,
			now);

	private Result EnsurePending() =>
		Status == InvitationStatus.Pending
			? Result.Success()
			: Result.Failure(Error.Conflict("OrganizationInvitation.NotPending", "Invitation is not pending."));

	public Result Accept()
	{
		var pending = EnsurePending();
		if (pending.IsFailure)
			return pending;

		Status = InvitationStatus.Accepted;
		AddEvent(new OrganizationInvitationAcceptedDomainEvent(Id, OrganizationId, InviteeId));
		return Result.Success();
	}

	public Result Decline()
	{
		var pending = EnsurePending();
		if (pending.IsFailure)
			return pending;

		Status = InvitationStatus.Declined;
		AddEvent(new OrganizationInvitationDeclinedDomainEvent(Id, OrganizationId, InviteeId));
		return Result.Success();
	}

	public Result Expire(DateTimeOffset now)
	{
		var pending = EnsurePending();
		if (pending.IsFailure)
			return pending;

		if (now < ExpiresOn)
			return Result.Failure(Error.Conflict("OrganizationInvitation.NotYetExpired", "Invitation has not reached its expiry date yet."));

		Status = InvitationStatus.Expired;
		return Result.Success();
	}

	public Result Resend(DateTimeOffset now)
	{
		if (Status != InvitationStatus.Expired)
			return Result.Failure(Error.Conflict("OrganizationInvitation.NotExpired", "Only expired invitations can be resent."));

		Status = InvitationStatus.Pending;
		ExpiresOn = now.AddDays(ExpiryWindowDays);
		return Result.Success();
	}
}
