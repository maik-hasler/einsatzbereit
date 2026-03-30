using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed class Engagement
    : AggregateRoot<EngagementId>,
      IAuditableEntity
{
    public VolunteerOpportunityId OpportunityId { get; private set; }

    public UserId VolunteerId { get; private set; }

    public TimeSlotId? TimeSlotId { get; private set; }

    public string? Message { get; private set; }

    public EngagementStatus Status { get; private set; }

    public DateTimeOffset CreatedOn { get; private set; }

    public DateTimeOffset? ModifiedOn { get; private set; }

    #pragma warning disable CS8618
    private Engagement() : base(default) { }
    #pragma warning restore CS8618

    private Engagement(
        EngagementId id,
        VolunteerOpportunityId opportunityId,
        UserId volunteerId,
        TimeSlotId? timeSlotId,
        string? message,
        EngagementStatus status)
        : base(id)
    {
        OpportunityId = opportunityId;
        VolunteerId = volunteerId;
        TimeSlotId = timeSlotId;
        Message = message;
        Status = status;
    }

    public static Engagement CreateWaitlistSignUp(
        VolunteerOpportunityId opportunityId,
        UserId volunteerId,
        TimeSlotId timeSlotId)
    {
        return new Engagement(
            new EngagementId(Guid.CreateVersion7()),
            opportunityId,
            volunteerId,
            timeSlotId,
            message: null,
            EngagementStatus.Pending);
    }

    public static Engagement CreateIndividualContact(
        VolunteerOpportunityId opportunityId,
        UserId volunteerId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new DomainException("A message is required when expressing interest via individual contact.");

        return new Engagement(
            new EngagementId(Guid.CreateVersion7()),
            opportunityId,
            volunteerId,
            timeSlotId: null,
            message,
            EngagementStatus.Pending);
    }

    public void Confirm()
    {
        if (Status != EngagementStatus.Pending)
            throw new DomainException("Only pending engagements can be confirmed.");

        Status = EngagementStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status is EngagementStatus.Withdrawn or EngagementStatus.Cancelled)
            throw new DomainException("Engagement is already terminated.");

        Status = EngagementStatus.Cancelled;
    }

    public void Withdraw()
    {
        if (Status is EngagementStatus.Cancelled or EngagementStatus.Withdrawn)
            throw new DomainException("Engagement is already terminated.");

        Status = EngagementStatus.Withdrawn;
    }
}
