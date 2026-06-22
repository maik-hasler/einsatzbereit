using Application.Common.Messaging;
using Application.VolunteerOpportunities;

namespace Application.Organizations.GetOrganizationCalendarEvents.v1;

internal sealed class GetOrganizationCalendarEventsQueryHandler(
	IVolunteerOpportunityReadRepository readRepository)
	: IQueryHandler<GetOrganizationCalendarEventsQuery, IReadOnlyList<OrganizationCalendarEventDto>>
{
	public async ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> Handle(
		GetOrganizationCalendarEventsQuery request,
		CancellationToken cancellationToken = default)
	{
		return await readRepository.GetCalendarEventsAsync(
			request.OrganizationId,
			cancellationToken);
	}
}
