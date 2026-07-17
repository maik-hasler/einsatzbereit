namespace Domain.Primitives;

public abstract class Entity<TId>(
	TId id)
	where TId : struct
{
	public TId Id { get; } = id;

	public override bool Equals(object? obj) =>
		obj is Entity<TId> other &&
		GetType() == other.GetType() &&
		Id.Equals(other.Id);

	public override int GetHashCode() =>
		HashCode.Combine(GetType(), Id);

	public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
		left?.Equals(right) ?? right is null;

	public static bool operator !=(Entity<TId>? left, Entity<TId>? right) =>
		!(left == right);
}
