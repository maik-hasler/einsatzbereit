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
			.Where(vo => !vo.TimeSlots.Any() || vo.TimeSlots.Any(ts => ts.EndDateTime >= now))
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new { vo, org });

		if (!string.IsNullOrWhiteSpace(filter.City))
		{
			var city = filter.City.ToLower();
			query = query.Where(x => x.vo.Address != null && x.vo.Address.City.ToLower().Contains(city));
		}

		if (!string.IsNullOrWhiteSpace(filter.Occurrence) && Enum.TryParse<Occurrence>(filter.Occurrence, ignoreCase: true, out var occ))
			query = query.Where(x => x.vo.Occurrence == occ);

		if (!string.IsNullOrWhiteSpace(filter.ParticipationType) && Enum.TryParse<ParticipationType>(filter.ParticipationType, ignoreCase: true, out var pt))
			query = query.Where(x => x.vo.ParticipationType == pt);

		if (filter.IsRemote is bool isRemote)
			query = query.Where(x => x.vo.IsRemote == isRemote);

		// Opportunities without time slots (IndividualContact - see VolunteerOpportunity.AddTimeSlot)
		// have no dates to compare against, so a date filter must not exclude them - matches the
		// same "slot-less is never filtered out" convention already used for expiry above (#1059).
		if (filter.DateFrom is DateTimeOffset dateFrom)
			query = query.Where(x => !x.vo.TimeSlots.Any() || x.vo.TimeSlots.Any(ts => ts.StartDateTime >= dateFrom));

		if (filter.DateTo is DateTimeOffset dateTo)
			query = query.Where(x => !x.vo.TimeSlots.Any() || x.vo.TimeSlots.Any(ts => ts.StartDateTime <= dateTo));

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
				query = query.Where(x => x.vo.Category.HasValue && parsedCategories.Contains(x.vo.Category.Value));
		}

		if (!string.IsNullOrWhiteSpace(filter.Tag))
			query = query.Where(x => x.vo.Tags.Contains(filter.Tag));

		var boundingBox = ResolveBoundingBox(filter);

		if (boundingBox is GeoBoundingBox box)
			query = query.Where(x =>
				x.vo.Address != null &&
				x.vo.Address.Latitude != null && x.vo.Address.Longitude != null &&
				x.vo.Address.Latitude >= box.South && x.vo.Address.Latitude <= box.North &&
				x.vo.Address.Longitude >= box.West && x.vo.Address.Longitude <= box.East);

		var baseQuery = query
			.OrderByDescending(x => x.vo.CreatedOn)
			.Select(x => new
			{
				Id = x.vo.Id.Value,
				x.vo.Title,
				x.vo.Description,
				OrganizationId = x.vo.OrganizationId.Value,
				OrgName = x.org.Name,
				OrgLogoUrl = x.org.LogoUrl,
				Street = x.vo.Address != null ? x.vo.Address.Street : null,
				HouseNumber = x.vo.Address != null ? x.vo.Address.HouseNumber : null,
				ZipCode = x.vo.Address != null ? x.vo.Address.ZipCode : null,
				City = x.vo.Address != null ? x.vo.Address.City : null,
				Latitude = x.vo.Address != null ? x.vo.Address.Latitude : null,
				Longitude = x.vo.Address != null ? x.vo.Address.Longitude : null,
				x.vo.IsRemote,
				x.vo.Occurrence,
				x.vo.ParticipationType,
				x.vo.CheckInMethod,
				x.vo.Category,
				x.vo.Tags,
				x.vo.CreatedOn,
				NextTimeSlotStart = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.StartDateTime)
					.FirstOrDefault(),
				NextTimeSlotEnd = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.EndDateTime)
					.FirstOrDefault(),
				x.vo.Status,
				x.vo.BannerImageUrl,
			});

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
				.Select(x => ToSummary(x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName, x.OrgLogoUrl,
					x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
					x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.NextTimeSlotStart, x.NextTimeSlotEnd,
					x.Status, x.BannerImageUrl,
					maxPMap.GetValueOrDefault(x.Id, 0), partCountMap.GetValueOrDefault(x.Id, 0)))
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
			.Select(x => ToSummary(x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName, x.OrgLogoUrl,
				x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
				x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.NextTimeSlotStart, x.NextTimeSlotEnd,
				x.Status, x.BannerImageUrl,
				maxParticipantsMap.GetValueOrDefault(x.Id, 0), participantCountMap.GetValueOrDefault(x.Id, 0)))
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

	private async Task<(Dictionary<Guid, int?> MaxParticipants, Dictionary<Guid, int> ParticipantCounts)>
		LoadParticipantStatsAsync(
			List<Guid> opportunityGuids,
			CancellationToken cancellationToken)
	{
		var opportunityIds = opportunityGuids
			.Select(g => VolunteerOpportunityId.Create(g).GetValueOrThrow())
			.ToList();

		// Null means "at least one time slot on this opportunity is uncapped" -
		// distinct from 0, which means no time slots at all (e.g. IndividualContact).
		var maxParticipants = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => opportunityIds.Contains(vo.Id))
			.Select(vo => new
			{
				OpportunityId = vo.Id.Value,
				MaxParticipants = vo.TimeSlots.Any(ts => ts.MaxParticipants == null)
					? (int?)null
					: vo.TimeSlots.Sum(ts => ts.MaxParticipants) ?? 0,
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

	// Shared post-materialization mapping only - the ~20-field EF projection itself stays
	// duplicated in each query below. Introducing a named type for the Join/Select result
	// broke EF Core's column pruning (it fell back to selecting every column of both
	// entities and fully materializing them client-side) - see #869 follow-up.
	private static VolunteerOpportunitySummary ToSummary(
		Guid id,
		string title,
		string description,
		Guid organizationId,
		string orgName,
		string? orgLogoUrl,
		string? street,
		string? houseNumber,
		string? zipCode,
		string? city,
		double? latitude,
		double? longitude,
		bool isRemote,
		Occurrence occurrence,
		ParticipationType participationType,
		CheckInMethod checkInMethod,
		Category? category,
		IReadOnlyList<string> tags,
		DateTimeOffset createdOn,
		DateTimeOffset? nextTimeSlotStart,
		DateTimeOffset? nextTimeSlotEnd,
		OpportunityStatus status,
		string? bannerImageUrl,
		int? totalMaxParticipants,
		int currentParticipantCount) =>
		new(
			id,
			title,
			description,
			organizationId,
			orgName,
			street,
			houseNumber,
			zipCode,
			city,
			latitude,
			longitude,
			isRemote,
			occurrence.ToString(),
			participationType.ToString(),
			checkInMethod.ToString(),
			category?.ToString(),
			tags,
			createdOn,
			nextTimeSlotStart,
			nextTimeSlotEnd,
			totalMaxParticipants,
			currentParticipantCount,
			status.ToString(),
			bannerImageUrl,
			orgLogoUrl);

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

		if (result.Status != OpportunityStatus.Published)
		{
			var isOrganizer = requestingUserId is Guid requestingUserId_ &&
				await dbContext.IsOrganizerAsync(
					result.OrganizationId,
					UserId.Create(requestingUserId_).GetValueOrThrow(),
					cancellationToken);

			if (!isOrganizer)
				return null;
		}

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
					(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed)),
				ts.SeriesId,
				ts.RecurrenceFrequency,
				ts.RecurrenceCount))
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
		var now = DateTimeOffset.UtcNow;

		var orgQuery = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == organizationId_);

		if (status is OpportunityStatus s)
			orgQuery = orgQuery.Where(vo => vo.Status == s);

		var rows = await orgQuery
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new { vo, org })
			.OrderByDescending(x => x.vo.CreatedOn)
			.Select(x => new
			{
				Id = x.vo.Id.Value,
				x.vo.Title,
				x.vo.Description,
				OrganizationId = x.vo.OrganizationId.Value,
				OrgName = x.org.Name,
				OrgLogoUrl = x.org.LogoUrl,
				Street = x.vo.Address != null ? x.vo.Address.Street : null,
				HouseNumber = x.vo.Address != null ? x.vo.Address.HouseNumber : null,
				ZipCode = x.vo.Address != null ? x.vo.Address.ZipCode : null,
				City = x.vo.Address != null ? x.vo.Address.City : null,
				Latitude = x.vo.Address != null ? x.vo.Address.Latitude : null,
				Longitude = x.vo.Address != null ? x.vo.Address.Longitude : null,
				x.vo.IsRemote,
				x.vo.Occurrence,
				x.vo.ParticipationType,
				x.vo.CheckInMethod,
				x.vo.Category,
				x.vo.Tags,
				x.vo.CreatedOn,
				NextTimeSlotStart = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.StartDateTime)
					.FirstOrDefault(),
				NextTimeSlotEnd = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.EndDateTime)
					.FirstOrDefault(),
				x.vo.Status,
				x.vo.BannerImageUrl,
			})
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return [];

		var guids = rows.Select(x => x.Id).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);

		return rows
			.Select(x => ToSummary(x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName, x.OrgLogoUrl,
				x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
				x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.NextTimeSlotStart, x.NextTimeSlotEnd,
				x.Status, x.BannerImageUrl,
				maxParticipantsMap.GetValueOrDefault(x.Id, 0), participantCountMap.GetValueOrDefault(x.Id, 0)))
			.ToList();
	}

	public async ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus status,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var organizationId_ = OrganizationId.Create(organizationId).GetValueOrThrow();
		var now = DateTimeOffset.UtcNow;

		var orgQuery = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == organizationId_ && vo.Status == status);

		var totalCount = await orgQuery.CountAsync(cancellationToken);

		var rows = await orgQuery
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new { vo, org })
			.OrderByDescending(x => x.vo.CreatedOn)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.Select(x => new
			{
				Id = x.vo.Id.Value,
				x.vo.Title,
				x.vo.Description,
				OrganizationId = x.vo.OrganizationId.Value,
				OrgName = x.org.Name,
				OrgLogoUrl = x.org.LogoUrl,
				Street = x.vo.Address != null ? x.vo.Address.Street : null,
				HouseNumber = x.vo.Address != null ? x.vo.Address.HouseNumber : null,
				ZipCode = x.vo.Address != null ? x.vo.Address.ZipCode : null,
				City = x.vo.Address != null ? x.vo.Address.City : null,
				Latitude = x.vo.Address != null ? x.vo.Address.Latitude : null,
				Longitude = x.vo.Address != null ? x.vo.Address.Longitude : null,
				x.vo.IsRemote,
				x.vo.Occurrence,
				x.vo.ParticipationType,
				x.vo.CheckInMethod,
				x.vo.Category,
				x.vo.Tags,
				x.vo.CreatedOn,
				NextTimeSlotStart = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.StartDateTime)
					.FirstOrDefault(),
				NextTimeSlotEnd = x.vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.EndDateTime)
					.FirstOrDefault(),
				x.vo.Status,
				x.vo.BannerImageUrl,
			})
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return new PagedList<VolunteerOpportunitySummary>([], totalCount, pageNumber, pageSize);

		var guids = rows.Select(x => x.Id).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);

		var items = rows
			.Select(x => ToSummary(x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName, x.OrgLogoUrl,
				x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
				x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.NextTimeSlotStart, x.NextTimeSlotEnd,
				x.Status, x.BannerImageUrl,
				maxParticipantsMap.GetValueOrDefault(x.Id, 0), participantCountMap.GetValueOrDefault(x.Id, 0)))
			.ToList();

		return new PagedList<VolunteerOpportunitySummary>(items, totalCount, pageNumber, pageSize);
	}

	public async ValueTask<IReadOnlyList<OrganizationCalendarEventDto>> GetCalendarEventsAsync(
		Guid organizationId,
		DateTimeOffset from,
		DateTimeOffset to,
		CancellationToken cancellationToken = default)
	{
		var orgId = OrganizationId.Create(organizationId).GetValueOrThrow();

		var rows = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == orgId)
			.Where(vo => vo.TimeSlots.Any(ts => ts.StartDateTime >= from && ts.StartDateTime <= to))
			.OrderBy(vo => vo.TimeSlots
				.Where(ts => ts.StartDateTime >= from && ts.StartDateTime <= to)
				.Min(ts => ts.StartDateTime))
			.Select(vo => new
			{
				Id = vo.Id.Value,
				vo.Title,
				vo.Color,
				TimeSlots = vo.TimeSlots
					.Where(ts => ts.StartDateTime >= from && ts.StartDateTime <= to)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => new { SlotId = ts.Id.Value, ts.StartDateTime, ts.EndDateTime, ts.MaxParticipants })
					.ToList(),
			})
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return [];

		var slotIds = rows
			.SelectMany(r => r.TimeSlots)
			.Select(ts => (TimeSlotId?)TimeSlotId.Create(ts.SlotId).GetValueOrThrow())
			.ToList();

		// Single query instead of a correlated Count(...) subquery per slot
		// (#1389) - but grouped client-side rather than via GroupBy(...).Value in
		// the query itself: EF Core can't reliably translate .Value/GroupBy on a
		// Nullable<TimeSlotId> sitting behind TimeSlotId's HasConversion (unlike
		// LoadParticipantStatsAsync's participantCounts query below, which groups
		// by the non-nullable OpportunityId and works fine). Fetching the raw
		// nullable values and counting them here keeps it to one round trip
		// without hitting that translation gap.
		var activeSlotIds = await dbContext.EngagementsQuery
			.Where(e => slotIds.Contains(e.TimeSlotId) &&
				(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))
			.Select(e => e.TimeSlotId)
			.ToListAsync(cancellationToken);

		var slotCounts = activeSlotIds
			.GroupBy(id => id!.Value.Value)
			.ToDictionary(g => g.Key, g => g.Count());

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
