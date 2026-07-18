using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

namespace Application.VolunteerOpportunities.GetOrganizationOpportunities.v1;

internal sealed class GetOrganizationOpportunitiesQueryHandler(
	IVolunteerOpportunityReadRepository readRepository,
	IApplicationDbContext dbContext)
	: IQueryHandler<GetOrganizationOpportunitiesQuery, IReadOnlyList<VolunteerOpportunitySummary>>
{
	public async ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> Handle(
		GetOrganizationOpportunitiesQuery request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		// status: null returns every status (Draft + Published), newest first -
		// this is the organizer's management view of all their opportunities.
		return await readRepository.GetSummariesByOrganizationAsync(
			request.OrganizationId,
			status: null,
			cancellationToken);
	}
}
