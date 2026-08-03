using Domain.Primitives;

namespace Domain.Users;

public sealed record UserDeletedDomainEvent(UserId UserId) : DomainEvent;
