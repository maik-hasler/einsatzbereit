using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.VolunteerOpportunities;

namespace Application.Engagements.GetEngagementCalendarInfo.v1;

internal sealed class GetEngagementCalendarInfoQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetEngagementCalendarInfoQuery, EngagementCalendarInfo?>
{
	public async ValueTask<EngagementCalendarInfo?> Handle(
		GetEngagementCalendarInfoQuery request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(
			new EngagementId(request.EngagementId), cancellationToken);

		if (engagement is null || engagement.TimeSlotId is null)
			return null;

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			engagement.OpportunityId, cancellationToken);

		var timeSlot = opportunity?.TimeSlots
			.FirstOrDefault(ts => ts.Id == engagement.TimeSlotId.Value);

		if (opportunity is null || timeSlot is null)
			return null;

		var location = opportunity.IsRemote
			? "Remote"
			: BuildAddress(opportunity.Address);

		return new EngagementCalendarInfo(
			engagement.Id.Value,
			opportunity.Id.Value,
			opportunity.Title,
			opportunity.Description,
			location,
			timeSlot.StartDateTime,
			timeSlot.EndDateTime);
	}

	private static string? BuildAddress(Address? address)
	{
		if (address is null)
			return null;

		var parts = new List<string>();
		var streetLine = $"{address.Street} {address.HouseNumber}".Trim();
		if (!string.IsNullOrWhiteSpace(streetLine))
			parts.Add(streetLine);
		var cityLine = $"{address.ZipCode} {address.City}".Trim();
		if (!string.IsNullOrWhiteSpace(cityLine))
			parts.Add(cityLine);
		return string.Join(", ", parts);
	}
}
