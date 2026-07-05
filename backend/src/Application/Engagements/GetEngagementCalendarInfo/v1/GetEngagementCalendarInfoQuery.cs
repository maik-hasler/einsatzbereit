using Application.Common.Messaging;

namespace Application.Engagements.GetEngagementCalendarInfo.v1;

public sealed record GetEngagementCalendarInfoQuery(Guid EngagementId)
	: IQuery<EngagementCalendarInfo?>;
