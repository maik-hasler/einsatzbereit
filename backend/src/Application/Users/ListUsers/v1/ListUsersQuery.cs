using Application.Common.Keycloak;
using Application.Common.Messaging;

namespace Application.Users.ListUsers.v1;

public sealed record ListUsersQuery(string? Search) : IQuery<IReadOnlyList<AdminUserListItem>>;
