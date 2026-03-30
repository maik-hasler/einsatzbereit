using Application.Abstractions;
using Application.Messaging;
using Application.Pagination;

namespace Application.Bedarfe.GetBedarfe.v1;

internal sealed class GetBedarfeQueryHandler(
    IApplicationDbContext dbContext)
    : IRequestHandler<GetBedarfeQuery, PagedList<BedarfSummary>>
{
    public async ValueTask<PagedList<BedarfSummary>> Handle(
        GetBedarfeQuery request,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.BedarfeQuery
            .OrderByDescending(b => b.CreatedOn)
            .Join(
                dbContext.OrganisationenQuery,
                bedarf => bedarf.OrganisationId,
                organisation => organisation.Id,
                (bedarf, organisation) => new BedarfSummary(
                    bedarf.Id.Value,
                    bedarf.Title,
                    bedarf.Description,
                    organisation.Name,
                    bedarf.Adresse,
                    bedarf.Frequenz,
                    bedarf.CreatedOn))
            .ToPagedListAsync(request.PageNumber, request.PageSize, cancellationToken);
    }
}
