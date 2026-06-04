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

		var projected = query
			.OrderByDescending(x => x.vo.CreatedOn)
			.Select(x => new VolunteerOpportunitySummary(
				x.vo.Id.Value,
				x.vo.Title,
				x.vo.Description,
				x.vo.OrganizationId.Value,
				x.org.Name,
				x.vo.Address != null ? x.vo.Address.Street : null,
				x.vo.Address != null ? x.vo.Address.HouseNumber : null,
				x.vo.Address != null ? x.vo.Address.ZipCode : null,
				x.vo.Address != null ? x.vo.Address.City : null,
				x.vo.Address != null ? x.vo.Address.Latitude : null,
				x.vo.Address != null ? x.vo.Address.Longitude : null,
				x.vo.IsRemote,
				x.vo.Occurrence.ToString(),
				x.vo.ParticipationType.ToString(),
				x.vo.CheckInMethod.ToString(),
				x.vo.Category != null ? x.vo.Category.ToString() : null,
				x.vo.Tags,
				x.vo.CreatedOn,
				x.vo.TimeSlots.Sum(ts => (int?)ts.MaxParticipants) ?? 0,
				dbContext.EngagementsQuery.Count(e =>
					e.OpportunityId == x.vo.Id &&
					(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))));

		if (filter.HasRadius)
		{
			var candidates = await projected.ToListAsync(cancellationToken);

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

			return new PagedList<VolunteerOpportunitySummary>(page, matched.Count, filter.PageNumber, filter.PageSize);
		}

		return await projected.ToPagedListAsync(filter.PageNumber, filter.PageSize, cancellationToken);
	}

	private static GeoBoundingBox? ResolveBoundingBox(VolunteerOpportunityFilter filter)
	{
		if (filter.HasRadius)
			return GeoMath.BoundingBoxFor(filter.CenterLatitude!.Value, filter.CenterLongitude!.Value, filter.RadiusKm!.Value);

		if (filter.HasBoundingBox)
			return new GeoBoundingBox(filter.South!.Value, filter.North!.Value, filter.West!.Value, filter.East!.Value);

		return null;
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
				x.vo.CreatedOn
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
			currentParticipantCount);
	}

	public async ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> GetSummariesByOrganizationAsync(
		Guid organizationId,
		CancellationToken cancellationToken = default)
	{
		var organizationId_ = new OrganizationId(organizationId);

		return await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => vo.OrganizationId == organizationId_)
			.Join(
				dbContext.OrganizationsQuery,
				vo => vo.OrganizationId,
				org => org.Id,
				(vo, org) => new { vo, org })
			.OrderByDescending(x => x.vo.CreatedOn)
			.Select(x => new VolunteerOpportunitySummary(
				x.vo.Id.Value,
				x.vo.Title,
				x.vo.Description,
				x.vo.OrganizationId.Value,
				x.org.Name,
				x.vo.Address != null ? x.vo.Address.Street : null,
				x.vo.Address != null ? x.vo.Address.HouseNumber : null,
				x.vo.Address != null ? x.vo.Address.ZipCode : null,
				x.vo.Address != null ? x.vo.Address.City : null,
				x.vo.Address != null ? x.vo.Address.Latitude : null,
				x.vo.Address != null ? x.vo.Address.Longitude : null,
				x.vo.IsRemote,
				x.vo.Occurrence.ToString(),
				x.vo.ParticipationType.ToString(),
				x.vo.CheckInMethod.ToString(),
				x.vo.Category != null ? x.vo.Category.ToString() : null,
				x.vo.Tags,
				x.vo.CreatedOn,
				x.vo.TimeSlots.Sum(ts => (int?)ts.MaxParticipants) ?? 0,
				dbContext.EngagementsQuery.Count(e =>
					e.OpportunityId == x.vo.Id &&
					(e.Status == EngagementStatus.Pending || e.Status == EngagementStatus.Confirmed))))
			.ToListAsync(cancellationToken);
	}
}
