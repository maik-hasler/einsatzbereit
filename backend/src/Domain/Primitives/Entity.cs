namespace Domain.Primitives;

public abstract class Entity<TId>(
    TId id)
    where TId : struct
{
    public TId Id { get; protected set; } = id;
}
