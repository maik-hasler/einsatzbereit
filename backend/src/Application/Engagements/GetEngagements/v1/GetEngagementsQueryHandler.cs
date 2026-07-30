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
	IKeycloakUserService keycloakUserService,
	IKeycloakOrganizationService keycloakOrganizationService)
	: IQueryHandler<GetEngagementsQuery, PagedList<EngagementSummary>>
{
	private const int MaxPageSize = 100;

	// Realm-wide search, not scoped to this opportunity's organization - Keycloak
	// has no per-opportunity user index. A generous max keeps a common first/last
	// name from silently missing volunteers signed up for this opportunity; the
	// repository still scopes the actual engagement rows down to this opportunity.
	private const int SearchMaxResults = 200;

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

		List<Guid>? matchedVolunteerIds = null;
		if (!string.IsNullOrWhiteSpace(request.Search))
		{
			var matches = await keycloakOrganizationService.SearchUsersAsync(
				request.Search.Trim(),
				SearchMaxResults,
				cancellationToken);
			matchedVolunteerIds = matches.Select(m => m.UserId).ToList();

			if (matchedVolunteerIds.Count == 0)
				return new PagedList<EngagementSummary>([], 0, pageNumber, pageSize);
		}

		var page = await readRepository.GetPagedByOpportunityAsync(
			request.OpportunityId,
			pageNumber,
			pageSize,
			request.Status,
			request.TimeSlotId,
			matchedVolunteerIds,
			cancellationToken);

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
