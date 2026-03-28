using Domain.Organisationen;
using Domain.Primitives;

namespace Domain.Bedarfe;

public sealed class Bedarf
    : AggregateRoot<BedarfId>,
    IAuditableEntity
{
    public OrganisationId OrganisationId { get; }
    
    public string Title { get; private set; }
    
    public string Description { get; private set; }
    
    public DateTimeOffset CreatedOn { get; private set; }
    
    public DateTimeOffset? ModifiedOn { get; private set; }
    
    public Status Status { get; }
    
    private Bedarf(
        BedarfId id,
        OrganisationId organisationId,
        string title,
        string description,
        Status status)
        : base(id)
    {
        OrganisationId = organisationId;
        Title = title;
        Description = description;
        Status = status;
    }

    public static Bedarf Create(
        string title,
        string description,
        OrganisationId organisationId,
        Status status)
    {
        return new Bedarf(
            new BedarfId(Guid.CreateVersion7()),
            organisationId,
            title,
            description,
            status);
    }
    
    public Bedarf Publish(DateTimeOffset publishedOn)
        => Status.Publish(this, publishedOn);

    public Bedarf WithStatus(Status status)
        => new Bedarf(Id, OrganisationId, Title, Description, status);
}

public abstract class Status
{
    public abstract Bedarf Publish(Bedarf bedarf, DateTimeOffset publishedOn);
}

public sealed class Draft : Status
{
    public override Bedarf Publish(Bedarf bedarf, DateTimeOffset publishedOn)
    {
        return bedarf.WithStatus(new Published(publishedOn));
    }
}

public sealed class Published : Status
{
    public DateTimeOffset PublishedOn { get; }

    public Published(DateTimeOffset publishedOn)
    {
        PublishedOn = publishedOn;
    }

    public override Bedarf Publish(Bedarf bedarf, DateTimeOffset publishedOn)
    {
        throw new DomainException("Bereits veröffentlicht");
    }
}