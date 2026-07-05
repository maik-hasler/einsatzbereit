using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.GetEngagementCalendarInfo.v1;

internal sealed class GetEngagementCalendarInfoQueryHandler(
	IEngagementReadRepository readRepository)
	: IQueryHandler<GetEngagementCalendarInfoQuery, EngagementCalendarInfo?>
{
	public async ValueTask<EngagementCalendarInfo?> Handle(
		GetEngagementCalendarInfoQuery request,
		CancellationToken cancellationToken = default) =>
			await readRepository.GetCalendarInfoAsync(
				new EngagementId(request.EngagementId), cancellationToken);
}
