using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsQueryHandler(
	IEngagementReadRepository readRepository,
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<GetEngagementsQuery, PagedList<EngagementSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<EngagementSummary>> Handle(
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

		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		var page = await readRepository.GetPagedByOpportunityAsync(request.OpportunityId, pageNumber, pageSize, cancellationToken);

		var volunteerIds = page.Items
			.Where(e => e.VolunteerId is not null)
			.Select(e => e.VolunteerId!.Value)
			.Distinct()
			.ToList();
		var profileMap = await keycloakUserService.GetUserProfilesAsync(volunteerIds, cancellationToken);

		return page.Map(e =>
		{
			if (e.VolunteerId is not Guid volunteerId || !profileMap.TryGetValue(volunteerId, out var profile))
				return e;

			var name = profile.FirstName is not null || profile.LastName is not null
				? $"{profile.FirstName} {profile.LastName}".Trim()
				: profile.Username;

			return e with { VolunteerName = name, VolunteerEmail = profile.Email };
		});
	}
}
