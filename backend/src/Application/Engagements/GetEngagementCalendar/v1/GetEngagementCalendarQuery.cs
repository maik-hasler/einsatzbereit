using Application.Common.Messaging;

namespace Application.Engagements.GetEngagementCalendar.v1;

public sealed record GetEngagementCalendarQuery(Guid EngagementId, string BaseUrl)
	: IQuery<EngagementCalendarFile?>;
