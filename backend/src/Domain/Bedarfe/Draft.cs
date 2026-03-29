namespace Domain.Bedarfe;

public sealed class Draft : Status
{
    public override void Publish(Bedarf bedarf, DateTimeOffset publishedOn)
    {
        bedarf.ApplyPublished(publishedOn);
    }
}