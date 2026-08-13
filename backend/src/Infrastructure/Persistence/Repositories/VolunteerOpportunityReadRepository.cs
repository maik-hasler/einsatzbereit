using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Common.Sitemap;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Infrastructure.Persistence.Repositories;

internal sealed class VolunteerOpportunityReadRepository(
	ApplicationDbContext dbContext)
	: IVolunteerOpportunityReadRepository
{
	// Same Published + not-yet-expired filter as GetPagedSummariesAsync below (#1086) -
	// a sitemap entry for an opportunity that's no longer publicly listed would just be
	// a dead link to crawlers.
	public async ValueTask<IReadOnlyList<SitemapEntry>> GetPublishedForSitemapAsync(
		CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;

		return await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Status == OpportunityStatus.Published)
			.Where(vo => vo.TimeSlots.Any(ts => ts.EndDateTime >= now) || (!vo.TimeSlots.Any() && vo.ValidUntil != null && vo.ValidUntil >= now))
			.Select(vo => new SitemapEntry(vo.Id.Value, vo.ModifiedOn ?? vo.CreatedOn))
			.ToListAsync(cancellationToken);
	}

	public async ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
		VolunteerOpportunityFilter filter,
		CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;

		// No Join to Organizations here - org name/logo are looked up separately below,
		// for just the page actually returned, keeping this query focused on filtering
		// and paging VolunteerOpportunity rows.
		var query = ApplyPubliclyListedFilters(
			dbContext.VolunteerOpportunitiesQuery,
			now,
			filter.Occurrence,
			filter.ParticipationType,
			filter.IsRemote,
			filter.Categories,
			filter.Tag,
			filter.Keyword,
			ResolveBoundingBox(filter.CenterLatitude, filter.CenterLongitude, filter.RadiusKm));

		// Opportunities without time slots (IndividualContact - see VolunteerOpportunity.AddTimeSlot)
		// have no dates to compare against, so a date filter must not exclude them - matches the
		// same "slot-less is never filtered out" convention already used for expiry above (#1059).
		if (filter.DateFrom is DateTimeOffset dateFrom)
			query = query.Where(vo => !vo.TimeSlots.Any() || vo.TimeSlots.Any(ts => ts.StartDateTime >= dateFrom));

		if (filter.DateTo is DateTimeOffset dateTo)
			query = query.Where(vo => !vo.TimeSlots.Any() || vo.TimeSlots.Any(ts => ts.StartDateTime <= dateTo));

		// Order on the entity itself, before the Select below - ordering by a
		// value-object id's unwrapped .Value (whether accessed directly in the key
		// selector or read back off an already-projected anonymous property) defeats
		// the Npgsql provider's translation of the whole query ("could not be
		// translated"). Ordering by the value object (vo.Id) instead translates
		// cleanly, since EF already has a converter registered for it. For the same
		// reason, Id is kept as VolunteerOpportunityId (not unwrapped to .Value) all
		// the way through this projection and the radius branch's below - the radius
		// branch's own ThenBy(s => s.Id) tie-breaker re-derives whatever expression
		// backs s.Id from the original entity, so unwrapping here would resurface the
		// exact same translation failure one layer down. Callers below unwrap to
		// .Value only after materialization (ToListAsync), in memory.
		var baseQuery = query
			.OrderByDescending(vo => vo.CreatedOn)
			.ThenBy(vo => vo.Id)
			.Select(vo => new
			{
				vo.Id,
				vo.Title,
				vo.Description,
				OrganizationId = vo.OrganizationId.Value,
				Street = vo.Address != null ? vo.Address.Street : null,
				HouseNumber = vo.Address != null ? vo.Address.HouseNumber : null,
				ZipCode = vo.Address != null ? vo.Address.ZipCode : null,
				City = vo.Address != null ? vo.Address.City : null,
				Latitude = vo.Address != null ? vo.Address.Latitude : null,
				Longitude = vo.Address != null ? vo.Address.Longitude : null,
				vo.IsRemote,
				vo.Occurrence,
				vo.ParticipationType,
				vo.CheckInMethod,
				vo.Category,
				vo.Tags,
				vo.CreatedOn,
				vo.ValidUntil,
				NextTimeSlotStart = vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.StartDateTime)
					.FirstOrDefault(),
				NextTimeSlotEnd = vo.TimeSlots
					.Where(ts => ts.EndDateTime >= now)
					.OrderBy(ts => ts.StartDateTime)
					.Select(ts => (DateTimeOffset?)ts.EndDateTime)
					.FirstOrDefault(),
				vo.Status,
				vo.BannerImageUrl,
			});

		if (filter.HasRadius)
		{
			var centerLat = filter.CenterLatitude!.Value;
			var centerLon = filter.CenterLongitude!.Value;
			var radiusKm = filter.RadiusKm!.Value;

			// Distance is computed once here (haversine formula, #1729) and referenced
			// in both the Where below and the OrderBy further down - filtering,
			// ordering, and paging all happen in Postgres instead of materializing the
			// whole bounding box into memory and redoing all three client-side on every
			// request/page.
			var withDistance = baseQuery
				.Where(s => s.Latitude.HasValue && s.Longitude.HasValue)
				.Select(s => new
				{
					s.Id,
					s.Title,
					s.Description,
					s.OrganizationId,
					s.Street,
					s.HouseNumber,
					s.ZipCode,
					s.City,
					s.Latitude,
					s.Longitude,
					s.IsRemote,
					s.Occurrence,
					s.ParticipationType,
					s.CheckInMethod,
					s.Category,
					s.Tags,
					s.CreatedOn,
					s.ValidUntil,
					s.NextTimeSlotStart,
					s.NextTimeSlotEnd,
					s.Status,
					s.BannerImageUrl,
					DistanceKm = 6371.0 * 2.0 * Math.Atan2(
						Math.Sqrt(Math.Pow(Math.Sin((s.Latitude!.Value - centerLat) * Math.PI / 180.0 / 2.0), 2) +
							Math.Cos(centerLat * Math.PI / 180.0) * Math.Cos(s.Latitude.Value * Math.PI / 180.0) *
							Math.Pow(Math.Sin((s.Longitude!.Value - centerLon) * Math.PI / 180.0 / 2.0), 2)),
						Math.Sqrt(1.0 - (Math.Pow(Math.Sin((s.Latitude.Value - centerLat) * Math.PI / 180.0 / 2.0), 2) +
							Math.Cos(centerLat * Math.PI / 180.0) * Math.Cos(s.Latitude.Value * Math.PI / 180.0) *
							Math.Pow(Math.Sin((s.Longitude.Value - centerLon) * Math.PI / 180.0 / 2.0), 2)))),
				})
				.Where(s => s.DistanceKm <= radiusKm);

			var matchedCount = await withDistance.CountAsync(cancellationToken);

			var page = await withDistance
				.OrderBy(s => s.DistanceKm)
				.ThenBy(s => s.Id)
				.Skip((filter.PageNumber - 1) * filter.PageSize)
				.Take(filter.PageSize)
				.ToListAsync(cancellationToken);

			var pageGuids = page.Select(x => x.Id.Value).ToList();
			var (maxPMap, partCountMap) = await LoadParticipantStatsAsync(pageGuids, cancellationToken);
			var orgMap = await LoadOrganizationSummariesAsync(page.Select(x => x.OrganizationId), cancellationToken);

			// Mirrors the inner-join semantics the previous single-query version had -
			// an opportunity whose organization is gone (deleted between the two
			// queries, or a data-integrity gap) is silently excluded rather than
			// shown with a missing organization name.
			var summaries = page
				.Where(x => orgMap.ContainsKey(x.OrganizationId))
				.Select(x =>
				{
					var (orgName, orgLogoUrl) = orgMap[x.OrganizationId];
					return ToSummary(x.Id.Value, x.Title, x.Description, x.OrganizationId, orgName, orgLogoUrl,
						x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
						x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.ValidUntil, x.NextTimeSlotStart, x.NextTimeSlotEnd,
						x.Status, x.BannerImageUrl,
						maxPMap.GetValueOrDefault(x.Id.Value, 0), partCountMap.GetValueOrDefault(x.Id.Value, 0));
				})
				.ToList();

			return new PagedList<VolunteerOpportunitySummary>(summaries, matchedCount, filter.PageNumber, filter.PageSize);
		}

		var total = await query.CountAsync(cancellationToken);
		var rows = await baseQuery
			.Skip((filter.PageNumber - 1) * filter.PageSize)
			.Take(filter.PageSize)
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return new PagedList<VolunteerOpportunitySummary>([], total, filter.PageNumber, filter.PageSize);

		var guids = rows.Select(x => x.Id.Value).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);
		var organizationSummaries = await LoadOrganizationSummariesAsync(rows.Select(x => x.OrganizationId), cancellationToken);

		// See the HasRadius branch above for why rows without a matching org are skipped.
		var result = rows
			.Where(x => organizationSummaries.ContainsKey(x.OrganizationId))
			.Select(x =>
			{
				var (orgName, orgLogoUrl) = organizationSummaries[x.OrganizationId];
				return ToSummary(x.Id.Value, x.Title, x.Description, x.OrganizationId, orgName, orgLogoUrl,
					x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude, x.IsRemote, x.Occurrence,
					x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.ValidUntil, x.NextTimeSlotStart, x.NextTimeSlotEnd,
					x.Status, x.BannerImageUrl,
					maxParticipantsMap.GetValueOrDefault(x.Id.Value, 0), participantCountMap.GetValueOrDefault(x.Id.Value, 0));
			})
			.ToList();

		return new PagedList<VolunteerOpportunitySummary>(result, total, filter.PageNumber, filter.PageSize);
	}

	public async ValueTask<IReadOnlyList<VolunteerOpportunityAvailableDate>> GetDateAvailabilityAsync(
		VolunteerOpportunityDateAvailabilityFilter filter,
		CancellationToken cancellationToken = default)
	{
		var now = DateTimeOffset.UtcNow;

		var query = ApplyPubliclyListedFilters(
			dbContext.VolunteerOpportunitiesQuery,
			now,
			filter.Occurrence,
			filter.ParticipationType,
			filter.IsRemote,
			filter.Categories,
			filter.Tag,
			filter.Keyword,
			ResolveBoundingBox(filter.CenterLatitude, filter.CenterLongitude, filter.RadiusKm));

		// One row per (opportunity, slot) in the window instead of a GROUP BY on a day
		// expression: which calendar day an instant falls on depends on the caller's UTC
		// offset, and there is no dependable Npgsql translation for date_trunc over a
		// shifted timestamptz to group by. The endpoint caps the window at ~2 months, so
		// what this materializes stays in the order of one calendar page of time slots.
		//
		// Matches GetPagedSummariesAsync's date filter exactly - on StartDateTime only,
		// with no second look at EndDateTime. A slot that started earlier today and has
		// already ended still puts today in the listing's results (its opportunity is
		// only listed at all if some other slot is still ahead), so it has to put today
		// in this answer too or the calendar would deny a day the list then fills.
		var slots = await query
			.SelectMany(vo => vo.TimeSlots
				.Where(ts => ts.StartDateTime >= filter.From && ts.StartDateTime <= filter.To)
				.Select(ts => new
				{
					OpportunityId = vo.Id.Value,
					ts.StartDateTime,
					Latitude = vo.Address != null ? vo.Address.Latitude : null,
					Longitude = vo.Address != null ? vo.Address.Longitude : null,
				}))
			.ToListAsync(cancellationToken);

		// The bounding box above is the cheap DB-side narrowing; the exact haversine
		// distance is the same second pass the radius branch of GetPagedSummariesAsync
		// makes, just run in memory here over the already-bounded window rather than
		// duplicating that inline formula a second time (GeoMath is the shared one).
		var withinRadius = filter.HasRadius
			? slots.Where(s => s.Latitude.HasValue && s.Longitude.HasValue &&
				GeoMath.DistanceKm(
					filter.CenterLatitude!.Value,
					filter.CenterLongitude!.Value,
					s.Latitude.Value,
					s.Longitude.Value) <= filter.RadiusKm!.Value)
			: slots;

		var offset = TimeSpan.FromMinutes(filter.UtcOffsetMinutes);

		return withinRadius
			.GroupBy(s => DateOnly.FromDateTime(s.StartDateTime.ToOffset(offset).DateTime))
			.OrderBy(g => g.Key)
			.Select(g => new VolunteerOpportunityAvailableDate(
				g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				g.Select(s => s.OpportunityId).Distinct().Count()))
			.ToList();
	}

	private async Task<Dictionary<Guid, (string Name, string? LogoUrl)>> LoadOrganizationSummariesAsync(
		IEnumerable<Guid> organizationGuids,
		CancellationToken cancellationToken)
	{
		var organizationIds = organizationGuids
			.Distinct()
			.Select(g => OrganizationId.Create(g).GetValueOrThrow())
			.ToList();

		if (organizationIds.Count == 0)
			return [];

		return await dbContext.OrganizationsQuery
			.Where(o => organizationIds.Contains(o.Id))
			.Select(o => new { o.Id, o.Name, o.LogoUrl })
			.ToDictionaryAsync(x => x.Id.Value, x => (x.Name, x.LogoUrl), cancellationToken);
	}

	// Every clause the public listing and the date-availability calendar have to agree
	// on, so an opportunity that can't show up in the results can't put a dot on a day
	// cell either (#1779). Paging and the picked date range stay with their callers -
	// those are the two things the two queries deliberately differ on.
	private IQueryable<VolunteerOpportunity> ApplyPubliclyListedFilters(
		IQueryable<VolunteerOpportunity> source,
		DateTimeOffset now,
		string? occurrence,
		string? participationType,
		bool? isRemote,
		string[]? categories,
		string? tag,
		string? keyword,
		GeoBoundingBox? boundingBox)
	{
		var query = source
			.Where(vo => vo.Status == OpportunityStatus.Published)
			// ScheduledSlots opportunities expire once their last time slot ends.
			// IndividualContact opportunities can never have time slots (see
			// VolunteerOpportunity.AddTimeSlot) and instead expire via ValidUntil -
			// a null ValidUntil (a legacy row published before ValidUntil existed)
			// is treated as already expired rather than kept forever (#1086).
			.Where(vo => vo.TimeSlots.Any(ts => ts.EndDateTime >= now) || (!vo.TimeSlots.Any() && vo.ValidUntil != null && vo.ValidUntil >= now));

		if (!string.IsNullOrWhiteSpace(occurrence) && Enum.TryParse<Occurrence>(occurrence, ignoreCase: true, out var occ))
			query = query.Where(vo => vo.Occurrence == occ);

		if (!string.IsNullOrWhiteSpace(participationType) && Enum.TryParse<ParticipationType>(participationType, ignoreCase: true, out var pt))
			query = query.Where(vo => vo.ParticipationType == pt);

		if (isRemote is bool isRemoteValue)
			query = query.Where(vo => vo.IsRemote == isRemoteValue);

		if (categories is { Length: > 0 })
		{
			var parsedCategories = categories
				.Select(c => Enum.TryParse<Domain.VolunteerOpportunities.Category>(c, ignoreCase: true, out var cat)
					? (Domain.VolunteerOpportunities.Category?)cat
					: null)
				.Where(c => c.HasValue)
				.Select(c => c!.Value)
				.ToList();

			if (parsedCategories.Count > 0)
				query = query.Where(vo => vo.Category.HasValue && parsedCategories.Contains(vo.Category.Value));
		}

		if (!string.IsNullOrWhiteSpace(tag))
			query = query.Where(vo => vo.Tags.Contains(tag));

		// Same .ToLower().Contains(...) pattern as OrganizationReadRepository's
		// own (now-removed) name search - Npgsql translates this to a
		// parameterized "lower(column) LIKE '%...%'", the established
		// convention in this repo for case-insensitive substring matching (no
		// EF.Functions.ILike usage exists elsewhere to follow instead). The
		// organization-name clause is an Any(...) subquery rather than a real
		// Join, specifically so it doesn't change query's element type (still
		// bare VolunteerOpportunity) - a real Join would ripple into every
		// vo.* reference in the caller's projection (see #869's comment) and
		// the HasRadius branch's CountAsync(query)/baseQuery reuse.
		if (!string.IsNullOrWhiteSpace(keyword))
		{
			var loweredKeyword = keyword.ToLower();
			query = query.Where(vo =>
				vo.Title.ToLower().Contains(loweredKeyword) ||
				vo.Description.ToLower().Contains(loweredKeyword) ||
				dbContext.OrganizationsQuery.Any(o => o.Id == vo.OrganizationId && o.Name.ToLower().Contains(loweredKeyword)));
		}

		if (boundingBox is GeoBoundingBox box)
			query = query.Where(vo =>
				vo.Address != null &&
				vo.Address.Latitude != null && vo.Address.Longitude != null &&
				vo.Address.Latitude >= box.South && vo.Address.Latitude <= box.North &&
				vo.Address.Longitude >= box.West && vo.Address.Longitude <= box.East);

		return query;
	}

	private static GeoBoundingBox? ResolveBoundingBox(double? centerLatitude, double? centerLongitude, double? radiusKm) =>
		centerLatitude.HasValue && centerLongitude.HasValue && radiusKm is > 0
			? GeoMath.BoundingBoxFor(centerLatitude.Value, centerLongitude.Value, radiusKm.Value)
			: null;

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
		DateTimeOffset? validUntil,
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
			validUntil,
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
				x.vo.ValidUntil,
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
			result.ValidUntil,
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
				x.vo.ValidUntil,
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
				x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.ValidUntil, x.NextTimeSlotStart, x.NextTimeSlotEnd,
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
			.ThenBy(x => x.vo.Id)
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
				x.vo.ValidUntil,
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
				x.ParticipationType, x.CheckInMethod, x.Category, x.Tags, x.CreatedOn, x.ValidUntil, x.NextTimeSlotStart, x.NextTimeSlotEnd,
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
