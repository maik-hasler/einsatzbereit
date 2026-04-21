using Application.Common.Pagination;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class VolunteerOpportunityReadRepository(
    ApplicationDbContext dbContext)
    : IVolunteerOpportunityReadRepository
{
    public async ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
        int pageNumber,
        int pageSize,
        string? search,
        string? city,
        string? occurrence,
        string? participationType,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.VolunteerOpportunitiesQuery
            .Join(
                dbContext.OrganizationsQuery,
                vo => vo.OrganizationId,
                org => org.Id,
                (vo, org) => new { vo, org });

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x =>
                x.vo.Title.ToLower().Contains(search.ToLower()) ||
                x.vo.Description.ToLower().Contains(search.ToLower()));

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(x => x.vo.Address != null && x.vo.Address.City.ToLower().Contains(city.ToLower()));

        if (!string.IsNullOrWhiteSpace(occurrence) && Enum.TryParse<Occurrence>(occurrence, ignoreCase: true, out var occ))
            query = query.Where(x => x.vo.Occurrence == occ);

        if (!string.IsNullOrWhiteSpace(participationType) && Enum.TryParse<ParticipationType>(participationType, ignoreCase: true, out var pt))
            query = query.Where(x => x.vo.ParticipationType == pt);

        return await query
            .OrderByDescending(x => x.vo.CreatedOn)
            .Select(x => new VolunteerOpportunitySummary(
                x.vo.Id.Value,
                x.vo.Title,
                x.vo.Description,
                x.org.Name,
                x.vo.Address != null ? x.vo.Address.Street : null,
                x.vo.Address != null ? x.vo.Address.HouseNumber : null,
                x.vo.Address != null ? x.vo.Address.ZipCode : null,
                x.vo.Address != null ? x.vo.Address.City : null,
                x.vo.IsRemote,
                x.vo.Occurrence.ToString(),
                x.vo.ParticipationType.ToString(),
                x.vo.CreatedOn))
            .ToPagedListAsync(pageNumber, pageSize, cancellationToken);
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
            result.IsRemote,
            result.Occurrence.ToString(),
            result.ParticipationType.ToString(),
            timeSlots,
            result.CreatedOn);
    }
}
