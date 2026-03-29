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

    public Adresse Adresse { get; private set; }

    public Frequenz Frequenz { get; private set; }

    public DateTimeOffset? PublishedOn { get; private set; }

    public Status Status => PublishedOn.HasValue
        ? new Published(PublishedOn.Value)
        : new Draft();

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset? ModifiedOn { get; private set; }
        
    #pragma warning disable CS8618 // EF Core constructor
    private Bedarf()
        : base(default) { }
    #pragma warning restore CS8618 // EF Core constructor

    private Bedarf(
        BedarfId id,
        OrganisationId organisationId,
        string title,
        string description,
        Adresse adresse,
        Frequenz frequenz)
        : base(id)
    {
        OrganisationId = organisationId;
        Title = title;
        Description = description;
        Adresse = adresse;
        Frequenz = frequenz;
    }

    public static Bedarf Create(
        string title,
        string description,
        OrganisationId organisationId,
        Adresse adresse,
        Frequenz frequenz)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Titel darf nicht leer sein.");

        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Beschreibung darf nicht leer sein.");

        ArgumentNullException.ThrowIfNull(adresse);

        return new Bedarf(
            new BedarfId(Guid.CreateVersion7()),
            organisationId,
            title,
            description,
            adresse,
            frequenz);
    }

    public void Publish(DateTimeOffset publishedOn)
        => Status.Publish(this, publishedOn);

    internal void ApplyPublished(DateTimeOffset publishedOn)
    {
        PublishedOn = publishedOn;
    }
}