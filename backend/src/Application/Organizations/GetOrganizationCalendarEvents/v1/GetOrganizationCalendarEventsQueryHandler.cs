using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.VolunteerOpportunities;

namespace Application.Organizations.GetOrganizationCalendarEvents.v1;

internal sealed class GetOrganizationCalendarEventsQueryHandler(
	IApplicationDbContext dbContext,
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetOrganizationCalendarEventsQuery, IReadOnlyList<OrganizationCalendarEventDto>>
{
	public async ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> Handle(
		GetOrganizationCalendarEventsQuery request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		return await readRepository.GetCalendarEventsAsync(
			request.OrganizationId,
			cancellationToken);
	}
}
