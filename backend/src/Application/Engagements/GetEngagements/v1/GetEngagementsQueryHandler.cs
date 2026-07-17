using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsQueryHandler(
	IEngagementReadRepository readRepository,
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<GetEngagementsQuery, List<EngagementSummary>>
{
	public async ValueTask<List<EngagementSummary>> Handle(
		GetEngagementsQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var engagements = await readRepository.GetByOpportunityAsync(request.OpportunityId, cancellationToken);

		var volunteerIds = engagements
			.Where(e => e.VolunteerId is not null)
			.Select(e => e.VolunteerId!.Value)
			.Distinct()
			.ToList();
		var nameMap = await keycloakUserService.GetDisplayNamesAsync(volunteerIds, cancellationToken);

		return engagements
			.Select(e => e with
			{
				VolunteerName = e.VolunteerId is Guid volunteerId
					? nameMap.GetValueOrDefault(volunteerId)
					: null,
			})
			.ToList();
	}
}
