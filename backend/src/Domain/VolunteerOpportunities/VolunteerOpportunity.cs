using Domain.Primitives;

namespace Domain.VolunteerOpportunities;

public sealed class VolunteerOpportunity
    : AggregateRoot<VolunteerOpportunityId>
{
    public OpportunityDetails Details { get; init; }
    
    public Location Location { get; init; }
    
    private VolunteerOpportunity(
        VolunteerOpportunityId id)
        : base(id)
    {
        
    }
}

public sealed record OpportunityDetails(
    string Title,
    string Description);