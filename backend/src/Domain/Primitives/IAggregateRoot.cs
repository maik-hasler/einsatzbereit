namespace Domain.Primitives;

public interface IAggregateRoot
{
    IReadOnlyCollection<DomainEvent> Events { get; }

    void ClearEvents();
}