namespace Domain.Primitives;

public abstract class AggregateRoot<TId>(
    TId id)
    : Entity<TId>(id),
        IAggregateRoot
where TId : struct
{
    private readonly List<DomainEvent> _events = [];

    public IReadOnlyCollection<DomainEvent> Events => _events;
    
    public void ClearEvents() => _events.Clear();
    
    protected void AddEvent(DomainEvent @event) => _events.Add(@event);
}