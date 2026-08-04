using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.GetOrganizationEngagements.v1;

internal sealed class GetOrganizationEngagementsQueryHandler(
	IEngagementReadRepository readRepository,
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService,
	IKeycloakOrganizationService keycloakOrganizationService)
	: IQueryHandler<GetOrganizationEngagementsQuery, PagedList<EngagementSummary>>
{
	private const int MaxPageSize = 100;

	// Same rationale as GetEngagementsQueryHandler - a realm-wide search, not
	// scoped to this organization, since Keycloak has no per-organization user
	// index. A generous max keeps a common first/last name from silently
	// missing a volunteer; the repository still scopes the actual engagement
	// rows down to this organization's opportunities.
	private const int SearchMaxResults = 200;

	public async ValueTask<PagedList<EngagementSummary>> Handle(
		GetOrganizationEngagementsQuery request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		_ = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
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

		var page = await readRepository.GetPagedByOrganizationAsync(
			organizationId,
			pageNumber,
			pageSize,
			request.Status,
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
