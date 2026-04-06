using Application.Common.Pagination;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Infrastructure.Persistence.Extensions;

namespace Infrastructure.Persistence.Repositories;

internal sealed class VolunteerOpportunityReadRepository(
    ApplicationDbContext dbContext)
    : IVolunteerOpportunityReadRepository
{
    public async ValueTask<PagedList<VolunteerOpportunitySummary>> GetPagedSummariesAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        await dbContext.VolunteerOpportunitiesQuery
            .OrderByDescending(vo => vo.CreatedOn)
            .Join(
                dbContext.OrganizationsQuery,
                vo => vo.OrganizationId,
                org => org.Id,
                (vo, org) => new { vo, org })
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
