using Domain.Primitives;

namespace Domain.Bedarfe;

public sealed class Published : Status
{
    public DateTimeOffset PublishedOn { get; }

    public Published(DateTimeOffset publishedOn)
    {
        PublishedOn = publishedOn;
    }

    public override void Publish(Bedarf bedarf, DateTimeOffset publishedOn)
    {
        throw new DomainException("Bereits veröffentlicht.");
    }
}