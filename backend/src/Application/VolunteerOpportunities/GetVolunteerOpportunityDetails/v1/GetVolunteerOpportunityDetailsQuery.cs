using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.GetVolunteerOpportunityDetails.v1;

public sealed record GetVolunteerOpportunityDetailsQuery(Guid OpportunityId, Guid? RequestingUserId = null)
	: IQuery<VolunteerOpportunityDetails?>;
