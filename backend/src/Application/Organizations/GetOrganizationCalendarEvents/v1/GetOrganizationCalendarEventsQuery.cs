using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetOrganizationCalendarEvents.v1;

public sealed record GetOrganizationCalendarEventsQuery(
	Guid OrganizationId,
	UserId RequestingUserId)
	: IQuery<IReadOnlyList<OrganizationCalendarEventDto>>;
