using Application.Common.Pagination;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
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

		if (!string.IsNullOrWhiteSpace(filter.Search))
		{
			var search = filter.Search.ToLower();
			query = query.Where(x =>
				x.vo.Title.ToLower().Contains(search) ||
				x.vo.Description.ToLower().Contains(search));
		}

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
				x.vo.CreatedOn));

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
				x.vo.CreatedOn
			})
			.FirstOrDefaultAsync(cancellationToken);

		if (result is null)
			return null;

		var timeSlots = await dbContext.TimeSlotsQuery
			.Where(ts => EF.Property<Guid>(ts, "volunteer_opportunity_id") == opportunityId)
			.OrderBy(ts => ts.StartDateTime)
			.Select(ts => new TimeSlotDetail(
				ts.Id.Value,
				ts.StartDateTime,
				ts.EndDateTime,
				ts.MaxParticipants))
			.ToListAsync(cancellationToken);

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
			timeSlots,
			result.CreatedOn);
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
				x.vo.CreatedOn))
			.ToListAsync(cancellationToken);
	}
}
