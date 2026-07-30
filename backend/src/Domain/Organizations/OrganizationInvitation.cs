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

	public DateTimeOffset ExpiresOn { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	// How long a Pending invitation stays actionable before InvitationExpiryJob
	// flips it to Expired. Shared by both Create (initial send) and Resend
	// (which restarts the same window) so the two paths can never drift apart.
	public const int ExpiryWindowDays = 14;

#pragma warning disable CS8618
	private OrganizationInvitation() : base(default) { }
#pragma warning restore CS8618

	private OrganizationInvitation(
		OrganizationInvitationId id,
		OrganizationId organizationId,
		string organizationName,
		UserId inviteeId,
		string inviteeName,
		UserId invitedById,
		DateTimeOffset now)
		: base(id)
	{
		OrganizationId = organizationId;
		OrganizationName = organizationName;
		InviteeId = inviteeId;
		InviteeName = inviteeName;
		InvitedById = invitedById;
		Status = InvitationStatus.Pending;
		ExpiresOn = now.AddDays(ExpiryWindowDays);
	}

	public static OrganizationInvitation Create(
		OrganizationId organizationId,
		string organizationName,
		UserId inviteeId,
		string inviteeName,
		UserId invitedById,
		DateTimeOffset now) =>
		new(
			OrganizationInvitationId.New(),
			organizationId,
			organizationName,
			inviteeId,
			inviteeName,
			invitedById,
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

	// Called by InvitationExpiryJob for every Pending invitation whose window
	// has elapsed (#1053). No domain event: unlike Accept/Decline this has no
	// interested subscriber - expiring is a silent cleanup, not something the
	// invitee or organizer needs to be told about the instant it happens.
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

	// Only an Expired invitation can be resent (#1053) - a still-Pending one
	// already has a live 14-day window running, and Accepted/Declined are
	// final. This also doubles as the only rate limit resend needs: the
	// window this restarts must itself elapse again before another resend is
	// possible, so an organizer can't spam the invitee's inbox on demand.
	public Result Resend(DateTimeOffset now)
	{
		if (Status != InvitationStatus.Expired)
			return Result.Failure(Error.Conflict("OrganizationInvitation.NotExpired", "Only expired invitations can be resent."));

		Status = InvitationStatus.Pending;
		ExpiresOn = now.AddDays(ExpiryWindowDays);
		return Result.Success();
	}
}
