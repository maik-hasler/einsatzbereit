using Domain.Primitives;

namespace Domain.Bedarfe;

public sealed class Bedarf
    : Entity<BedarfId>,
    IAuditableEntity
{
    public string Title { get; private set; }
    
    public string Description { get; private set; }
    
    public DateTimeOffset CreatedOn { get; }
    
    public DateTimeOffset? ModifiedOn { get; }
    
    private Bedarf(
        BedarfId id,
        string title,
        string description)
        : base(id)
    {
        Title = title;
        Description = description;
    }

    public static Bedarf Create(
        string title,
        string description)
    {
        return new Bedarf(
            new BedarfId(Guid.CreateVersion7()),
            title,
            description);
    }
}