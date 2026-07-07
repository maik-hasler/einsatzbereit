using Application.Engagements;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class EngagementReadRepository(
	ApplicationDbContext dbContext)
	: IEngagementReadRepository
{
	public async ValueTask<List<EngagementSummary>> GetByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default)
	{
		var raw = await dbContext.EngagementsQuery
			.Where(e => e.OpportunityId == opportunityId)
			.Join(
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title, o.OrganizationId }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					o.OrganizationId,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.FeedbackSubmittedAt,
					e.CreatedOn,
				})
			.Join(
				dbContext.OrganizationsQuery.Select(org => new { org.Id, org.Name }),
				x => x.OrganizationId,
				org => org.Id,
				(x, org) => new
				{
					x.Id,
					x.OpportunityId,
					x.OpportunityTitle,
					OrganizationId = org.Id,
					OrganizationName = org.Name,
					x.VolunteerId,
					x.TimeSlotId,
					x.Message,
					x.Status,
					x.IsCheckedIn,
					x.FeedbackSubmittedAt,
					x.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.OrganizationId.Value,
			x.OrganizationName,
			x.VolunteerId.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.FeedbackSubmittedAt.HasValue,
			x.CreatedOn)).ToList();
	}

	public async ValueTask<List<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default)
	{
		var raw = await dbContext.EngagementsQuery
			.Where(e => e.VolunteerId == volunteerId)
			.Join(
				dbContext.VolunteerOpportunitiesQuery.Select(o => new { o.Id, o.Title, o.IsRemote, o.Address, o.OrganizationId }),
				e => e.OpportunityId,
				o => o.Id,
				(e, o) => new
				{
					e.Id,
					e.OpportunityId,
					OpportunityTitle = o.Title,
					o.IsRemote,
					o.Address,
					o.OrganizationId,
					e.VolunteerId,
					e.TimeSlotId,
					e.Message,
					e.Status,
					e.IsCheckedIn,
					e.FeedbackSubmittedAt,
					e.CreatedOn,
				})
			.Join(
				dbContext.OrganizationsQuery.Select(org => new { org.Id, org.Name }),
				x => x.OrganizationId,
				org => org.Id,
				(x, org) => new
				{
					x.Id,
					x.OpportunityId,
					x.OpportunityTitle,
					x.IsRemote,
					x.Address,
					OrganizationId = org.Id,
					OrganizationName = org.Name,
					x.VolunteerId,
					x.TimeSlotId,
					x.Message,
					x.Status,
					x.IsCheckedIn,
					x.FeedbackSubmittedAt,
					x.CreatedOn,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		var timeSlotIds = raw
			.Where(x => x.TimeSlotId is not null)
			.Select(x => x.TimeSlotId!.Value)
			.Distinct()
			.ToList();

		Dictionary<TimeSlotId, TimeSlot> timeSlots = [];
		if (timeSlotIds.Count > 0)
		{
			timeSlots = await dbContext.TimeSlotsQuery
				.Where(ts => timeSlotIds.Contains(ts.Id))
				.ToDictionaryAsync(ts => ts.Id, cancellationToken);
		}

		return raw.Select(x =>
		{
			TimeSlot? timeSlot = x.TimeSlotId is not null
				&& timeSlots.TryGetValue(x.TimeSlotId.Value, out var slot)
					? slot
					: null;

			var location = x.IsRemote ? "Remote" : FormatAddress(x.Address);

			return new EngagementSummary(
				x.Id.Value,
				x.OpportunityId.Value,
				x.OpportunityTitle,
				x.OrganizationId.Value,
				x.OrganizationName,
				x.VolunteerId.Value,
				x.TimeSlotId?.Value,
				x.Message,
				x.Status.ToString(),
				x.IsCheckedIn,
				x.FeedbackSubmittedAt.HasValue,
				x.CreatedOn,
				TimeSlotStartDateTime: timeSlot?.StartDateTime,
				TimeSlotEndDateTime: timeSlot?.EndDateTime,
				Location: location);
		}).ToList();
	}

	private static string? FormatAddress(Address? address)
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

	public async ValueTask<EngagementCalendarInfo?> GetCalendarInfoAsync(
		EngagementId engagementId,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.EngagementsQuery
			.Where(e => e.Id == engagementId)
			.Select(e => new { e.OpportunityId, e.TimeSlotId })
			.FirstOrDefaultAsync(cancellationToken);

		if (engagement is null || engagement.TimeSlotId is null)
			return null;

		var opportunity = await dbContext.VolunteerOpportunitiesQuery
			.Where(o => o.Id == engagement.OpportunityId)
			.Select(o => new { o.Id, o.Title, o.Description, o.IsRemote, o.Address })
			.FirstOrDefaultAsync(cancellationToken);

		if (opportunity is null)
			return null;

		var timeSlot = await dbContext.TimeSlotsQuery
			.Where(ts => ts.Id == engagement.TimeSlotId.Value)
			.Select(ts => new { ts.StartDateTime, ts.EndDateTime })
			.FirstOrDefaultAsync(cancellationToken);

		if (timeSlot is null)
			return null;

		var location = opportunity.IsRemote ? "Remote" : FormatAddress(opportunity.Address);

		return new EngagementCalendarInfo(
			engagementId.Value,
			opportunity.Id.Value,
			opportunity.Title,
			opportunity.Description,
			location,
			timeSlot.StartDateTime,
			timeSlot.EndDateTime);
	}

	public async ValueTask<OpportunityFeedbackSummary> GetFeedbackByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		CancellationToken cancellationToken = default)
	{
		var items = await dbContext.EngagementsQuery
			.Where(e => e.OpportunityId == opportunityId && e.FeedbackSubmittedAt != null)
			.OrderByDescending(e => e.FeedbackSubmittedAt)
			.Select(e => new FeedbackItemDto(
				e.FeedbackRating!.Value,
				e.FeedbackComment,
				e.FeedbackSubmittedAt!.Value))
			.ToListAsync(cancellationToken);

		var avg = items.Count > 0 ? (double?)items.Average(f => f.Rating) : null;
		return new OpportunityFeedbackSummary(avg, items.Count, items);
	}
}
