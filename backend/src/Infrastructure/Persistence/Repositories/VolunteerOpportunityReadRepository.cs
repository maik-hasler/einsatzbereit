using System.Linq.Expressions;
using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class VolunteerOpportunityReadRepository(
	ApplicationDbContext dbContext)
	: IVolunteerOpportunityReadRepository
{
	public async ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
		VolunteerOpportunityFilter filter,
		CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;

		var query = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Status == OpportunityStatus.Published)
			.Where(vo => !vo.TimeSlots.Any() || vo.TimeSlots.Any(ts => ts.EndDateTime >= now));

		if (!string.IsNullOrWhiteSpace(filter.City))
		{
			var city = filter.City.ToLower();
			query = query.Where(vo => vo.Address != null && vo.Address.City.ToLower().Contains(city));
		}

		if (!string.IsNullOrWhiteSpace(filter.Occurrence) && Enum.TryParse<Occurrence>(filter.Occurrence, ignoreCase: true, out var occ))
			query = query.Where(vo => vo.Occurrence == occ);

		if (!string.IsNullOrWhiteSpace(filter.ParticipationType) && Enum.TryParse<ParticipationType>(filter.ParticipationType, ignoreCase: true, out var pt))
			query = query.Where(vo => vo.ParticipationType == pt);

		if (filter.IsRemote is bool isRemote)
			query = query.Where(vo => vo.IsRemote == isRemote);

		if (filter.DateFrom is DateTimeOffset dateFrom)
			query = query.Where(vo => vo.TimeSlots.Any(ts => ts.StartDateTime >= dateFrom));

		if (filter.DateTo is DateTimeOffset dateTo)
			query = query.Where(vo => vo.TimeSlots.Any(ts => ts.StartDateTime <= dateTo));

		if (filter.Categories is { Length: > 0 })
		{
			var parsedCategories = filter.Categories
				.Select(c => Enum.TryParse<Domain.VolunteerOpportunities.Category>(c, ignoreCase: true, out var cat)
					? (Domain.VolunteerOpportunities.Category?)cat
					: null)
				.Where(c => c.HasValue)
				.Select(c => c!.Value)
				.ToList();

			if (parsedCategories.Count > 0)
				query = query.Where(vo => vo.Category.HasValue && parsedCategories.Contains(vo.Category.Value));
		}

		if (!string.IsNullOrWhiteSpace(filter.Tag))
			query = query.Where(vo => vo.Tags.Contains(filter.Tag));

		var boundingBox = ResolveBoundingBox(filter);

		if (boundingBox is GeoBoundingBox box)
			query = query.Where(vo =>
				vo.Address != null &&
				vo.Address.Latitude != null && vo.Address.Longitude != null &&
				vo.Address.Latitude >= box.South && vo.Address.Latitude <= box.North &&
				vo.Address.Longitude >= box.West && vo.Address.Longitude <= box.East);

		var baseQuery = query
			.OrderByDescending(vo => vo.CreatedOn)
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new VoOrgPair(vo, org))
			.Select(ToSummaryRow);

		if (filter.HasRadius)
		{
			var candidates = await baseQuery.ToListAsync(cancellationToken);

			var centerLat = filter.CenterLatitude!.Value;
			var centerLon = filter.CenterLongitude!.Value;
			var radiusKm = filter.RadiusKm!.Value;

			var matched = candidates
				.Where(s => s.Latitude.HasValue && s.Longitude.HasValue &&
					GeoMath.DistanceKm(centerLat, centerLon, s.Latitude!.Value, s.Longitude!.Value) <= radiusKm)
				.OrderBy(s => GeoMath.DistanceKm(centerLat, centerLon, s.Latitude!.Value, s.Longitude!.Value))
				.ToList();

			var page = matched
				.Skip((filter.PageNumber - 1) * filter.PageSize)
				.Take(filter.PageSize)
				.ToList();

			var pageGuids = page.Select(x => x.Id).ToList();
			var (maxPMap, partCountMap) = await LoadParticipantStatsAsync(pageGuids, cancellationToken);

			var summaries = page
				.Select(x => ToSummary(x, maxPMap.GetValueOrDefault(x.Id, 0), partCountMap.GetValueOrDefault(x.Id, 0)))
				.ToList();

			return new PagedList<VolunteerOpportunitySummary>(summaries, matched.Count, filter.PageNumber, filter.PageSize);
		}

		var total = await query.CountAsync(cancellationToken);
		var rows = await baseQuery
			.Skip((filter.PageNumber - 1) * filter.PageSize)
			.Take(filter.PageSize)
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return new PagedList<VolunteerOpportunitySummary>([], total, filter.PageNumber, filter.PageSize);

		var guids = rows.Select(x => x.Id).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);

		var result = rows
			.Select(x => ToSummary(x, maxParticipantsMap.GetValueOrDefault(x.Id, 0), participantCountMap.GetValueOrDefault(x.Id, 0)))
			.ToList();

		return new PagedList<VolunteerOpportunitySummary>(result, total, filter.PageNumber, filter.PageSize);
	}

	private static GeoBoundingBox? ResolveBoundingBox(VolunteerOpportunityFilter filter)
	{
		if (filter.HasRadius)
			return GeoMath.BoundingBoxFor(filter.CenterLatitude!.Value, filter.CenterLongitude!.Value, filter.RadiusKm!.Value);

		if (filter.HasBoundingBox)
			return new GeoBoundingBox(filter.South!.Value, filter.North!.Value, filter.West!.Value, filter.East!.Value);

		return null;
	}

	private async Task<(Dictionary<Guid, int> MaxParticipants, Dictionary<Guid, int> ParticipantCounts)>
		LoadParticipantStatsAsync(
			List<Guid> opportunityGuids,
			CancellationToken cancellationToken)
	{
		var opportunityIds = opportunityGuids
			.Select(g => VolunteerOpportunityId.Create(g).GetValueOrThrow())
			.ToList();

		var maxParticipants = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => opportunityIds.Contains(vo.Id))
			.Select(vo => new
			{
				OpportunityId = vo.Id.Value,
				MaxParticipants = vo.TimeSlots.Sum(ts => (int?)ts.MaxParticipants) ?? 0,
			})
			.ToListAsync(cancellationToken);

		var participantCounts = await dbContext.EngagementsQuery
			.Where(e => opportunityIds.Contains(e.OpportunityId) &&
				(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
			.GroupBy(e => e.OpportunityId)
			.Select(g => new { OpportunityId = g.Key.Value, Count = g.Count() })
			.ToListAsync(cancellationToken);

		return (
			maxParticipants.ToDictionary(x => x.OpportunityId, x => x.MaxParticipants),
			participantCounts.ToDictionary(x => x.OpportunityId, x => x.Count)
		);
	}

	private sealed record VoOrgPair(VolunteerOpportunity Vo, Organization Org);

	private sealed record VolunteerOpportunitySummaryRow(
		Guid Id,
		string Title,
		string Description,
		Guid OrganizationId,
		string OrgName,
		bool OrgIsVerified,
		string? OrgLogoUrl,
		string? Street,
		string? HouseNumber,
		string? ZipCode,
		string? City,
		double? Latitude,
		double? Longitude,
		bool IsRemote,
		Occurrence Occurrence,
		ParticipationType ParticipationType,
		CheckInMethod CheckInMethod,
		Category? Category,
		IReadOnlyList<string> Tags,
		DateTimeOffset CreatedOn,
		OpportunityStatus Status,
		string? BannerImageUrl);

	private static readonly Expression<Func<VoOrgPair, VolunteerOpportunitySummaryRow>> ToSummaryRow =
		x => new VolunteerOpportunitySummaryRow(
			x.Vo.Id.Value,
			x.Vo.Title,
			x.Vo.Description,
			x.Vo.OrganizationId.Value,
			x.Org.Name,
			x.Org.IsVerified,
			x.Org.LogoUrl,
			x.Vo.Address != null ? x.Vo.Address.Street : null,
			x.Vo.Address != null ? x.Vo.Address.HouseNumber : null,
			x.Vo.Address != null ? x.Vo.Address.ZipCode : null,
			x.Vo.Address != null ? x.Vo.Address.City : null,
			x.Vo.Address != null ? x.Vo.Address.Latitude : null,
			x.Vo.Address != null ? x.Vo.Address.Longitude : null,
			x.Vo.IsRemote,
			x.Vo.Occurrence,
			x.Vo.ParticipationType,
			x.Vo.CheckInMethod,
			x.Vo.Category,
			x.Vo.Tags,
			x.Vo.CreatedOn,
			x.Vo.Status,
			x.Vo.BannerImageUrl);

	private static VolunteerOpportunitySummary ToSummary(
		VolunteerOpportunitySummaryRow row,
		int totalMaxParticipants,
		int currentParticipantCount) =>
		new(
			row.Id,
			row.Title,
			row.Description,
			row.OrganizationId,
			row.OrgName,
			row.Street,
			row.HouseNumber,
			row.ZipCode,
			row.City,
			row.Latitude,
			row.Longitude,
			row.IsRemote,
			row.Occurrence.ToString(),
			row.ParticipationType.ToString(),
			row.CheckInMethod.ToString(),
			row.Category?.ToString(),
			row.Tags,
			row.CreatedOn,
			totalMaxParticipants,
			currentParticipantCount,
			row.Status.ToString(),
			row.BannerImageUrl,
			row.OrgIsVerified,
			row.OrgLogoUrl);

	public async ValueTask<VolunteerOpportunityDetails?> GetDetailsAsync(
		Guid opportunityId,
		Guid? requestingUserId = null,
		CancellationToken cancellationToken = default)
	{
		var opportunityId_ = VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow();

		var result = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Id == opportunityId_)
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new { vo, org })
			.Select(x => new
			{
				x.vo.Id,
				x.vo.Title,
				x.vo.Description,
				x.vo.OrganizationId,
				x.org.Name,
				x.vo.Address,
				x.vo.IsRemote,
				x.vo.Occurrence,
				x.vo.ParticipationType,
				x.vo.CheckInMethod,
				x.vo.Category,
				x.vo.Tags,
				x.vo.CreatedOn,
				x.vo.Status,
				BannerImageUrl = x.vo.BannerImageUrl
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (result is null)
			return null;

		var timeSlots = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Id == opportunityId_)
			.SelectMany(vo => vo.TimeSlots)
			.OrderBy(ts => ts.StartDateTime)
			.Select(ts => new TimeSlotDetail(
				ts.Id.Value,
				ts.StartDateTime,
				ts.EndDateTime,
				ts.MaxParticipants,
				dbContext.EngagementsQuery.Count(e =>
					e.TimeSlotId == ts.Id &&
					(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))))
			.ToListAsync(cancellationToken);

		var currentParticipantCount = await dbContext.EngagementsQuery
			.CountAsync(e =>
				e.OpportunityId == opportunityId_ &&
				(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed),
				cancellationToken);

		CurrentUserEngagementInfo? currentUserEngagement = null;
		if (requestingUserId is Guid uid)
		{
			var userId_ = UserId.Create(uid).GetValueOrThrow();
			var engagement = await dbContext.EngagementsQuery
				.Where(e =>
					e.OpportunityId == opportunityId_ &&
					e.VolunteerId == userId_ &&
					(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
				.OrderByDescending(e => e.CreatedOn)
				.Select(e => new { e.Id, e.Status, e.TimeSlotId })
				.FirstOrDefaultAsync(cancellationToken);

			if (engagement is not null)
				currentUserEngagement = new CurrentUserEngagementInfo(
					engagement.Id.Value,
					engagement.Status.ToString(),
					engagement.TimeSlotId?.Value);
		}

		return new VolunteerOpportunityDetails(
			result.Id.Value,
			result.Title,
			result.Description,
			result.OrganizationId.Value,
			result.Name,
			result.Address?.Street,
			result.Address?.HouseNumber,
			result.Address?.ZipCode,
			result.Address?.City,
			result.Address?.Latitude,
			result.Address?.Longitude,
			result.IsRemote,
			result.Occurrence.ToString(),
			result.ParticipationType.ToString(),
			result.CheckInMethod.ToString(),
			result.Category?.ToString(),
			result.Tags,
			timeSlots,
			result.CreatedOn,
			currentParticipantCount,
			result.Status.ToString(),
			result.BannerImageUrl,
			currentUserEngagement);
	}

	public async ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus? status = null,
		CancellationToken cancellationToken = default)
	{
		var organizationId_ = OrganizationId.Create(organizationId).GetValueOrThrow();

		var orgQuery = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == organizationId_);

		if (status is OpportunityStatus s)
			orgQuery = orgQuery.Where(vo => vo.Status == s);

		var rows = await orgQuery
			.OrderByDescending(vo => vo.CreatedOn)
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new VoOrgPair(vo, org))
			.Select(ToSummaryRow)
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return [];

		var guids = rows.Select(x => x.Id).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);

		return rows
			.Select(x => ToSummary(x, maxParticipantsMap.GetValueOrDefault(x.Id, 0), participantCountMap.GetValueOrDefault(x.Id, 0)))
			.ToList();
	}

	public async ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> GetCalendarEventsAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		var orgId = OrganizationId.Create(organizationId).GetValueOrThrow();

		var rows = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Where(vo => vo.TimeSlots.Any())
			.OrderBy(vo => vo.TimeSlots.Min(ts => ts.StartDateTime))
			.Select(vo => new
			{
				Id = vo.Id.Value,
				vo.Title,
				vo.Color,
				TimeSlots = vo.TimeSlots
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => new { SlotId = ts.Id.Value, ts.StartDateTime, ts.EndDateTime, ts.MaxParticipants })
					.ToList(),
			})
			.ToListAsync(cancellationToken);

		var opportunityIds = rows.Select(r => VolunteerOpportunityId.Create(r.Id).GetValueOrThrow()).ToList();
		var slotCounts = opportunityIds.Count == 0
			? new Dictionary<Guid, int>()
			: await dbContext.VolunteerOpportunitiesQuery
				.Where(vo => opportunityIds.Contains(vo.Id))
				.SelectMany(vo => vo.TimeSlots)
				.Select(ts => new
				{
					SlotId = ts.Id.Value,
					BookedCount = dbContext.EngagementsQuery.Count(e =>
						e.TimeSlotId == ts.Id &&
						(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed)),
				})
				.ToDictionaryAsync(x => x.SlotId, x => x.BookedCount, cancellationToken);

		return rows
			.Select(r => new OrganizationCalendarEventDto(
				r.Id,
				r.Title,
				r.Color,
				r.TimeSlots
					.Select(ts => new CalendarTimeSlotDto(
						ts.SlotId,
						ts.StartDateTime,
						ts.EndDateTime,
						ts.MaxParticipants,
						slotCounts.GetValueOrDefault(ts.SlotId, 0)))
					.ToList()))
			.ToList();
	}
}
