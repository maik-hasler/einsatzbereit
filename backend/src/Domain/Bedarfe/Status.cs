namespace Domain.Bedarfe;

public abstract class Status
{
    public abstract void Publish(Bedarf bedarf, DateTimeOffset publishedOn);
}