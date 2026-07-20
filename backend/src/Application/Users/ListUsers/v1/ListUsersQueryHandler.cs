using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Users.ListUsers.v1;

internal sealed class ListUsersQueryHandler(
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<ListUsersQuery, PagedList<AdminUserListItem>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<AdminUserListItem>> Handle(
		ListUsersQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await keycloakUserService.ListUsersAsync(request.Search, pageNumber, pageSize, cancellationToken);
	}
}
