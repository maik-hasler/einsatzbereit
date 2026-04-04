using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class TimeSlot : Entity<TimeSlotId>
{
    public DateTimeOffset StartDateTime { get; private set; }

    public DateTimeOffset EndDateTime { get; private set; }

    public int MaxParticipants { get; private set; }

    #pragma warning disable CS8618
    private TimeSlot() : base(default) { }
    #pragma warning restore CS8618

    private TimeSlot(
        TimeSlotId id,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        int maxParticipants)
        : base(id)
    {
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        MaxParticipants = maxParticipants;
    }

    public static TimeSlot Create(
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        int maxParticipants)
    {
        if (endDateTime <= startDateTime)
            throw new DomainException("End date must be after start date.");

        if (maxParticipants <= 0)
            throw new DomainException("Max participants must be greater than zero.");

        return new TimeSlot(
            new TimeSlotId(Guid.CreateVersion7()),
            startDateTime,
            endDateTime,
            maxParticipants);
    }
}
