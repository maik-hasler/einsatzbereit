using Application.Common.Geocoding;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

internal sealed class UpdateVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository,
	IGeocodingService geocodingService,
	ILogger<UpdateVolunteerOpportunityCommandHandler> logger)
	: ICommandHandler<UpdateVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		if (request.ParticipationType != opportunity.ParticipationType)
		{
			var engagements = await engagementReadRepository.GetByOpportunityAsync(
				new VolunteerOpportunityId(request.OpportunityId), cancellationToken);

			var hasActiveEngagements = engagements.Any(e =>
				e.Status is "Pending" or "Confirmed");

			if (hasActiveEngagements)
				throw new DomainException(
					"ParticipationType cannot be changed while active engagements exist.");
		}

		var address = request.Address;

		if (!request.IsRemote && address is not null)
			address = await GeocodingHelper.EnrichAsync(address, geocodingService, logger, cancellationToken);

		opportunity.Update(
			request.Title,
			request.Description,
			request.IsRemote,
			address,
			request.Occurrence,
			request.ParticipationType,
			request.CheckInMethod);

		return true;
	}
}
