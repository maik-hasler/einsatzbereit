using Application.Common.Messaging;
using Application.Common.Pagination;
using Application.Common.Persistence;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;

internal sealed class GetVolunteerOpportunitiesQueryHandler(
    IApplicationDbContext dbContext)
    : IQueryHandler<GetVolunteerOpportunitiesQuery, PagedList<VolunteerOpportunitySummary>>
{
    public async ValueTask<PagedList<VolunteerOpportunitySummary>> Handle(
        GetVolunteerOpportunitiesQuery request,
        CancellationToken cancellationToken = default)
    {
        var joined = dbContext.VolunteerOpportunitiesQuery
            .OrderByDescending(vo => vo.CreatedOn)
            .Join(
                dbContext.OrganizationsQuery,
                vo => vo.OrganizationId,
                org => org.Id,
                (vo, org) => new { vo, org });

        var paged = await joined
            .Select(x => new { x.vo.Id, x.vo.Title, x.vo.Description, OrgName = x.org.Name, x.vo.Location, x.vo.Occurrence, x.vo.ParticipationType, x.vo.CreatedOn })
            .ToPagedListAsync(request.PageNumber, request.PageSize, cancellationToken);

        return paged.Map(x =>
        {
            var address = x.Location is PhysicalLocation pl ? pl.Address : null;
            return new VolunteerOpportunitySummary(
                x.Id.Value,
                x.Title,
                x.Description,
                x.OrgName,
                address?.Street,
                address?.HouseNumber,
                address?.ZipCode,
                address?.City,
                x.Location is RemoteLocation,
                x.Occurrence.ToString(),
                x.ParticipationType.ToString(),
                x.CreatedOn);
        });
    }
}
