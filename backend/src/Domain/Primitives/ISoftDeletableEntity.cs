namespace Domain.Primitives;

public interface ISoftDeletableEntity
{
	bool IsDeleted { get; }

	DateTimeOffset? DeletedOn { get; }
}
