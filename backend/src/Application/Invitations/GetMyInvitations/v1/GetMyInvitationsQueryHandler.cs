using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Invitations.GetMyInvitations.v1;

internal sealed class GetMyInvitationsQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetMyInvitationsQuery, List<MyInvitationDto>>
{
	public async ValueTask<List<MyInvitationDto>> Handle(
		GetMyInvitationsQuery request,
		CancellationToken cancellationToken = default)
	{
		var invitations = await dbContext.GetPendingInvitationsForUserAsync(
			request.UserId, cancellationToken);

		var organizationIds = invitations.Select(i => i.OrganizationId).Distinct().ToList();
		var organizationNames = await dbContext.GetOrganizationNamesAsync(organizationIds, cancellationToken);

		return invitations
			.Select(i => new MyInvitationDto(
				i.Id.Value,
				i.OrganizationId.Value,
				organizationNames.GetValueOrDefault(i.OrganizationId.Value, "(unknown organization)"),
				i.CreatedOn))
			.ToList();
	}
}
