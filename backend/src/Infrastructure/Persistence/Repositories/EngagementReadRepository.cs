using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Engagements;
using Domain.Common;
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
					e.CancellationReason,
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
					x.CancellationReason,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ToListAsync(cancellationToken);

		return raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.OrganizationId.Value,
			x.OrganizationName,
			x.VolunteerId?.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.FeedbackSubmittedAt.HasValue,
			x.CreatedOn,
			CancellationReason: x.CancellationReason)).ToList();
	}

	public async ValueTask<PagedList<EngagementSummary>> GetPagedByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		int pageNumber,
		int pageSize,
		EngagementStatus? status = null,
		TimeSlotId? timeSlotId = null,
		IReadOnlyList<Guid>? volunteerIds = null,
		CancellationToken cancellationToken = default)
	{
		var scopedQuery = dbContext.EngagementsQuery.Where(e => e.OpportunityId == opportunityId);

		if (status is not null)
			scopedQuery = scopedQuery.Where(e => e.Status == status.Value);

		// Nullable-to-nullable equality (no .Value unwrap on the nullable value-object
		// column) - the same shape already proven safe by
		// GetActiveVolunteerIdsByOpportunityAsync below.
		if (timeSlotId is not null)
			scopedQuery = scopedQuery.Where(e => e.TimeSlotId == timeSlotId);

		if (volunteerIds is not null)
		{
			// Candidate ids as List<UserId?> (not .Value-unwrapped) so Contains stays
			// translatable against the nullable value-object VolunteerId column - see
			// the EF Core nullable-value-object gotcha this repository already has to
			// work around for TimeSlotId.
			var candidateIds = volunteerIds
				.Select(id => (UserId?)UserId.Create(id).GetValueOrThrow())
				.ToList();
			scopedQuery = scopedQuery.Where(e => candidateIds.Contains(e.VolunteerId));
		}

		var totalCount = await scopedQuery.CountAsync(cancellationToken);

		var raw = await scopedQuery
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
					e.CancellationReason,
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
					x.CancellationReason,
				})
			.OrderByDescending(x => x.CreatedOn)
			.ThenBy(x => x.Id)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var pageVolunteerIds = raw
			.Where(x => x.VolunteerId is not null)
			.Select(x => x.VolunteerId!.Value)
			.Distinct()
			.ToList();

		Dictionary<Guid, string?> phonesByVolunteerId = [];
		if (pageVolunteerIds.Count > 0)
		{
			phonesByVolunteerId = await dbContext.UsersQuery
				.Where(u => pageVolunteerIds.Contains(u.Id))
				.ToDictionaryAsync(u => u.Id.Value, u => u.Phone, cancellationToken);
		}

		var items = raw.Select(x => new EngagementSummary(
			x.Id.Value,
			x.OpportunityId.Value,
			x.OpportunityTitle,
			x.OrganizationId.Value,
			x.OrganizationName,
			x.VolunteerId?.Value,
			x.TimeSlotId?.Value,
			x.Message,
			x.Status.ToString(),
			x.IsCheckedIn,
			x.FeedbackSubmittedAt.HasValue,
			x.CreatedOn,
			VolunteerPhone: x.VolunteerId is not null
				? phonesByVolunteerId.GetValueOrDefault(x.VolunteerId.Value.Value)
				: null,
			CancellationReason: x.CancellationReason)).ToList();

		return new PagedList<EngagementSummary>(items, totalCount, pageNumber, pageSize);
	}

	public async ValueTask<PagedList<EngagementSummary>> GetByVolunteerAsync(
		UserId volunteerId,
		bool upcoming,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;

		// Deliberately not an inner join against VolunteerOpportunitiesQuery: deleting an
		// opportunity hard-deletes its row while only cancelling (not deleting) the
		// volunteer's Engagement rows, so an inner join would silently drop those
		// engagements from the volunteer's own history (#667). Opportunity/organization
		// data is instead looked up separately and merged in below, falling back to
		// null when the opportunity no longer exists.
		//
		// The time slot join below IS a left join (an IndividualContact engagement has
		// no TimeSlotId at all), used only to bucket/order by the slot's dates - actual
		// time slot data for the response is still looked up in MapToSummariesAsync.
		var scopedQuery =
			from e in dbContext.EngagementsQuery
			where e.VolunteerId == volunteerId
			join ts in dbContext.TimeSlotsQuery on e.TimeSlotId equals ts.Id into tsGroup
			from ts in tsGroup.DefaultIfEmpty()
			select new { Engagement = e, TimeSlotStart = (DateTimeOffset?)ts.StartDateTime, TimeSlotEnd = (DateTimeOffset?)ts.EndDateTime };

		// Current/upcoming vs. past split (#675): a checked-in Confirmed engagement
		// represents a shift that has already happened, so it counts as past even
		// though its status is not yet terminal.
		//
		// opportunityExists is only used to reclassify bucket membership below,
		// not to join in opportunity data - that still happens separately further
		// down per the no-inner-join note above, so a deleted opportunity's
		// engagements keep appearing here rather than vanishing per #667.
		var opportunityExists = dbContext.VolunteerOpportunitiesQuery.Select(o => o.Id);

		// A non-terminal engagement whose opportunity was deleted can never be
		// confirmed, checked into, or otherwise acted on again, so it belongs in
		// Past rather than staying in "Current & Upcoming" forever (#703). Likewise
		// (#1163) a time slot that has already ended moves the engagement to Past
		// regardless of status/check-in - previously only IsCheckedIn could do that,
		// but check-in is optional (CheckInMethod.None has no check-in action at all),
		// so a shift nobody checked in for stayed "upcoming" permanently. An
		// engagement with no time slot (IndividualContact) is unaffected either way.
		scopedQuery = upcoming
			? scopedQuery.Where(x =>
				(x.Engagement.Status == EngagementStatus.Pending
					|| (x.Engagement.Status == EngagementStatus.Confirmed && !x.Engagement.IsCheckedIn))
				&& opportunityExists.Contains(x.Engagement.OpportunityId)
				&& (x.TimeSlotEnd == null || x.TimeSlotEnd >= now))
			: scopedQuery.Where(x =>
				x.Engagement.Status == EngagementStatus.Cancelled
				|| x.Engagement.Status == EngagementStatus.Withdrawn
				|| (x.Engagement.Status == EngagementStatus.Confirmed && x.Engagement.IsCheckedIn)
				|| ((x.Engagement.Status == EngagementStatus.Pending
						|| (x.Engagement.Status == EngagementStatus.Confirmed && !x.Engagement.IsCheckedIn))
					&& (!opportunityExists.Contains(x.Engagement.OpportunityId)
						|| (x.TimeSlotEnd != null && x.TimeSlotEnd < now))));

		var totalCount = await scopedQuery.CountAsync(cancellationToken);

		// Upcoming is ordered by the slot's own start time (soonest shift first,
		// #1163) rather than CreatedOn, which reflected sign-up order, not shift
		// order; entries with no time slot sort last. Both branches add the primary
		// key as a tiebreaker (#1161) so ties can't repeat/skip rows across pages.
		var orderedQuery = upcoming
			? scopedQuery.OrderBy(x => x.TimeSlotStart ?? DateTimeOffset.MaxValue).ThenBy(x => x.Engagement.Id)
			: scopedQuery.OrderByDescending(x => x.Engagement.CreatedOn).ThenBy(x => x.Engagement.Id);

		var engagements = await orderedQuery
			.Select(x => x.Engagement)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var items = await MapToSummariesAsync(engagements, cancellationToken);

		return new PagedList<EngagementSummary>(items, totalCount, pageNumber, pageSize);
	}

	public async ValueTask<List<EngagementSummary>> GetAllByVolunteerAsync(
		UserId volunteerId,
		CancellationToken cancellationToken = default)
	{
		// Same no-inner-join rationale as GetByVolunteerAsync above (#667) - a
		// deleted opportunity must not drop the volunteer's own engagement from
		// their data export.
		var engagements = await dbContext.EngagementsQuery
			.Where(e => e.VolunteerId == volunteerId)
			.OrderByDescending(e => e.CreatedOn)
			.ToListAsync(cancellationToken);

		return await MapToSummariesAsync(engagements, cancellationToken);
	}

	private async Task<List<EngagementSummary>> MapToSummariesAsync(
		List<Engagement> engagements,
		CancellationToken cancellationToken)
	{
		var opportunityIds = engagements.Select(e => e.OpportunityId).Distinct().ToList();
		var opportunities = await dbContext.VolunteerOpportunitiesQuery
			.Where(o => opportunityIds.Contains(o.Id))
			.Select(o => new { o.Id, o.Title, o.IsRemote, o.Address, o.OrganizationId })
			.ToDictionaryAsync(o => o.Id, cancellationToken);

		var organizationIds = opportunities.Values.Select(o => o.OrganizationId).Distinct().ToList();
		var organizations = await dbContext.OrganizationsQuery
			.Where(org => organizationIds.Contains(org.Id))
			.Select(org => new { org.Id, org.Name })
			.ToDictionaryAsync(org => org.Id, cancellationToken);

		var timeSlotIds = engagements
			.Where(e => e.TimeSlotId is not null)
			.Select(e => e.TimeSlotId!.Value)
			.Distinct()
			.ToList();

		Dictionary<TimeSlotId, TimeSlot> timeSlots = [];
		if (timeSlotIds.Count > 0)
		{
			timeSlots = await dbContext.TimeSlotsQuery
				.Where(ts => timeSlotIds.Contains(ts.Id))
				.ToDictionaryAsync(ts => ts.Id, cancellationToken);
		}

		return engagements.Select(e =>
		{
			opportunities.TryGetValue(e.OpportunityId, out var opportunity);
			var organization = opportunity is not null
				&& organizations.TryGetValue(opportunity.OrganizationId, out var org)
					? org
					: null;

			TimeSlot? timeSlot = e.TimeSlotId is not null
				&& timeSlots.TryGetValue(e.TimeSlotId.Value, out var slot)
					? slot
					: null;

			var location = opportunity is null
				? null
				: opportunity.IsRemote ? "Remote" : FormatAddress(opportunity.Address);

			return new EngagementSummary(
				e.Id.Value,
				e.OpportunityId.Value,
				opportunity?.Title,
				organization?.Id.Value,
				organization?.Name,
				e.VolunteerId?.Value,
				e.TimeSlotId?.Value,
				e.Message,
				e.Status.ToString(),
				e.IsCheckedIn,
				e.FeedbackSubmittedAt.HasValue,
				e.CreatedOn,
				TimeSlotStartDateTime: timeSlot?.StartDateTime,
				TimeSlotEndDateTime: timeSlot?.EndDateTime,
				Location: location,
				CancellationReason: e.CancellationReason);
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

		// Status == Published (#1155): this endpoint is anonymous, so without this an
		// unpublished Draft opportunity's title/description/address leaked to anyone
		// holding an engagement id for it - the one thing GetDetailsAsync already
		// refuses to show a non-organizer.
		var opportunity = await dbContext.VolunteerOpportunitiesQuery
			.Where(o => o.Id == engagement.OpportunityId && o.Status == OpportunityStatus.Published)
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

	public async ValueTask<List<Guid>> GetActiveVolunteerIdsByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		TimeSlotId? timeSlotId,
		CancellationToken cancellationToken = default)
	{
		var query = dbContext.EngagementsQuery
			.Where(e => e.OpportunityId == opportunityId
				&& e.VolunteerId != null
				&& (e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed));

		if (timeSlotId is not null)
			query = query.Where(e => e.TimeSlotId == timeSlotId);

		var volunteerIds = await query
			.Select(e => e.VolunteerId)
			.Distinct()
			.ToListAsync(cancellationToken);

		return volunteerIds.Select(id => id!.Value.Value).ToList();
	}

	public async ValueTask<OpportunityFeedbackSummary> GetFeedbackByOpportunityAsync(
		VolunteerOpportunityId opportunityId,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var scopedQuery = dbContext.EngagementsQuery
			.Where(e => e.OpportunityId == opportunityId && e.FeedbackSubmittedAt != null);

		var totalCount = await scopedQuery.CountAsync(cancellationToken);

		var avg = totalCount > 0
			? (double?)await scopedQuery.AverageAsync(e => e.FeedbackRating!.Value, cancellationToken)
			: null;

		var items = await scopedQuery
			.OrderByDescending(e => e.FeedbackSubmittedAt)
			.ThenBy(e => e.Id)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.Select(e => new FeedbackItemDto(
				e.FeedbackRating!.Value,
				e.FeedbackComment,
				e.FeedbackSubmittedAt!.Value))
			.ToListAsync(cancellationToken);

		return new OpportunityFeedbackSummary(avg, totalCount, new PagedList<FeedbackItemDto>(items, totalCount, pageNumber, pageSize));
	}
}
