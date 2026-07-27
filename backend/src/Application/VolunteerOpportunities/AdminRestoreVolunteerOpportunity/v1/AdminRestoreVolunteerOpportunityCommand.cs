using Application.Common.Messaging;

namespace Application.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;

public sealed record AdminRestoreVolunteerOpportunityCommand(
	Guid OpportunityId)
	: ICommand<bool>;
