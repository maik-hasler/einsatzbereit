namespace Domain.Primitives;

public interface IAuditableEntity
{
    DateTimeOffset CreatedOn { get; }
    
    DateTimeOffset? ModifiedOn { get; }
}