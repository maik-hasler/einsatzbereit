using Application.Messaging;
using Domain.Bedarfe;
using Domain.Primitives;

namespace Application.Bedarfe.GetBedarfe.v1;

internal sealed class GetBedarfeQueryHandler(
    IBedarfRepository bedarfRepository)
    : IRequestHandler<GetBedarfeQuery, PagedList<Bedarf>>
{
    public async ValueTask<PagedList<Bedarf>> Handle(
        GetBedarfeQuery request,
        CancellationToken cancellationToken = default)
    {
        return await bedarfRepository.GetAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);
    }
}