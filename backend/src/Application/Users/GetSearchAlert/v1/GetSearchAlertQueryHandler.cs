using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Users.GetSearchAlert.v1;

internal sealed class GetSearchAlertQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetSearchAlertQuery, SearchAlertResponse>
{
	public async ValueTask<SearchAlertResponse> Handle(
		GetSearchAlertQuery request,
		CancellationToken cancellationToken = default)
	{
		var alert = await dbContext.GetSearchAlertForUserAsync(request.UserId, cancellationToken);

		if (alert is null)
			return new SearchAlertResponse(false, null, null, null, null, null, null, [], null);

		return new SearchAlertResponse(
			true,
			alert.Occurrence?.ToString(),
			alert.ParticipationType?.ToString(),
			alert.IsRemote,
			alert.CenterLatitude,
			alert.CenterLongitude,
			alert.RadiusKm,
			alert.Categories,
			alert.Tag);
	}
}
