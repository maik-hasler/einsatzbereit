using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.GetOrganizationCalendarEvents.v1;

public sealed record GetOrganizationCalendarEventsQuery(
	Guid OrganizationId,
	UserId RequestingUserId,
	DateTimeOffset From,
	DateTimeOffset To)
	: IQuery<IReadOnlyList<OrganizationCalendarEventDto>>;
