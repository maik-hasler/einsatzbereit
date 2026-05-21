using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CheckInEngagement.v1;

internal sealed class CheckInEngagementCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CheckInEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CheckInEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		engagement.CheckIn();

		return engagement;
	}
}
