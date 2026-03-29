using Application.Messaging;
using Application.Pagination;
using Domain.Primitives;

namespace Application.Bedarfe.GetBedarfe.v1;

public sealed record GetBedarfeQuery(
    int PageNumber,
    int PageSize)
    : IRequest<PagedList<BedarfSummary>>;
