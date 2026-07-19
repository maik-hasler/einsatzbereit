using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.ListUsers.v1;

internal sealed class ListUsersQueryHandler(
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<ListUsersQuery, IReadOnlyList<AdminUserListItem>>
{
	public async ValueTask<IReadOnlyList<AdminUserListItem>> Handle(
		ListUsersQuery request,
		CancellationToken cancellationToken = default) =>
		await keycloakUserService.ListUsersAsync(request.Search, cancellationToken: cancellationToken);
}
