using Domain.Primitives;

namespace Domain.Users;

public sealed record UserAccountDeletedDomainEvent(
	UserId UserId)
	: DomainEvent;
