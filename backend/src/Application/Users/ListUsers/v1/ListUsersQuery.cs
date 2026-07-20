using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Users.ListUsers.v1;

public sealed record ListUsersQuery(
	string? Search,
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<AdminUserListItem>>;
