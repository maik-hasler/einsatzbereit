using Application.Common.Pagination;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.Engagements;
using Domain.Organizations;
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
		var query = dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Status == OpportunityStatus.Published)
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

		if (filter.DateFrom is DateTimeOffset dateFrom)
			query = query.Where(x => x.vo.TimeSlots.Any(ts => ts.StartDateTime >= dateFrom));

		if (filter.DateTo is DateTimeOffset dateTo)
			query = query.Where(x => x.vo.TimeSlots.Any(ts => ts.StartDateTime <= dateTo));

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
				.Select(x => new VolunteerOpportunitySummary(
					x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName,
					x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude,
					x.IsRemote, x.Occurrence.ToString(), x.ParticipationType.ToString(),
					x.CheckInMethod.ToString(), x.Category?.ToString(), x.Tags, x.CreatedOn,
					maxPMap.GetValueOrDefault(x.Id, 0),
					partCountMap.GetValueOrDefault(x.Id, 0),
					x.Status.ToString(), x.BannerImageUrl))
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
			.Select(x => new VolunteerOpportunitySummary(
				x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName,
				x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude,
				x.IsRemote, x.Occurrence.ToString(), x.ParticipationType.ToString(),
				x.CheckInMethod.ToString(), x.Category?.ToString(), x.Tags, x.CreatedOn,
				maxParticipantsMap.GetValueOrDefault(x.Id, 0),
				participantCountMap.GetValueOrDefault(x.Id, 0),
				x.Status.ToString(), x.BannerImageUrl))
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
			.Select(g => new VolunteerOpportunityId(g))
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

	public async ValueTask<VolunteerOpportunityDetails?> GetDetailsAsync(
		Guid opportunityId,
		CancellationToken cancellationToken = default)
	{
		var opportunityId_ = new VolunteerOpportunityId(opportunityId);

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
				x.vo.CheckInPin,
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
				ts.MaxParticipants))
			.ToListAsync(cancellationToken);

		var currentParticipantCount = await dbContext.EngagementsQuery
			.CountAsync(e =>
				e.OpportunityId == opportunityId_ &&
				(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed),
				cancellationToken);

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
			result.CheckInPin,
			timeSlots,
			result.CreatedOn,
			currentParticipantCount,
			result.Status.ToString(),
			result.BannerImageUrl);
	}

	public async ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		OpportunityStatus? status = null,
		CancellationToken cancellationToken = default)
	{
		var organizationId_ = new OrganizationId(organizationId);

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
				x.vo.Status,
				x.vo.BannerImageUrl,
			})
			.ToListAsync(cancellationToken);

		if (rows.Count == 0)
			return [];

		var guids = rows.Select(x => x.Id).ToList();
		var (maxParticipantsMap, participantCountMap) = await LoadParticipantStatsAsync(guids, cancellationToken);

		return rows
			.Select(x => new VolunteerOpportunitySummary(
				x.Id, x.Title, x.Description, x.OrganizationId, x.OrgName,
				x.Street, x.HouseNumber, x.ZipCode, x.City, x.Latitude, x.Longitude,
				x.IsRemote, x.Occurrence.ToString(), x.ParticipationType.ToString(),
				x.CheckInMethod.ToString(), x.Category?.ToString(), x.Tags, x.CreatedOn,
				maxParticipantsMap.GetValueOrDefault(x.Id, 0),
				participantCountMap.GetValueOrDefault(x.Id, 0),
				x.Status.ToString(), x.BannerImageUrl))
			.ToList();
	}

	public async ValueTask<string?> GetBannerUrlAsync(
		Guid opportunityId,
		CancellationToken cancellationToken = default)
	{
		var opportunityId_ = new VolunteerOpportunityId(opportunityId);

		return await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.Id == opportunityId_)
			.Select(vo => vo.BannerImageUrl)
			.FirstOrDefaultAsync(cancellationToken);
	}
}
