using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Reports.ListFlaggedTargets.v1;

public sealed record ListFlaggedTargetsQuery(
	int PageNumber,
	int PageSize,
	bool IncludeResolved)
	: IQuery<PagedList<FlaggedTargetSummary>>;
