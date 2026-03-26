using Domain.Primitives;

namespace Domain.Needs;

public sealed class Need
    : Entity<NeedId>
{
    public string Title { get; private set; }
    
    public string Description { get; private set; }
    
    private Need(
        NeedId id,
        string title,
        string description)
        : base(id)
    {
        Title = title;
        Description = description;
    }

    public static Need Create(
        string title,
        string description)
    {
        return new Need(
            new NeedId(Guid.CreateVersion7()),
            title,
            description);
    }
}