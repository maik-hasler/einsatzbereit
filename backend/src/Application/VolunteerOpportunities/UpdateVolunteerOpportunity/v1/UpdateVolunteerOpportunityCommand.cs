using Application.Common.Messaging;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;

public sealed record UpdateVolunteerOpportunityCommand(
	Guid OpportunityId,
	string Title,
	string Description,
	bool IsRemote,
	Address? Address,
	Occurrence Occurrence,
	ParticipationType ParticipationType,
	CheckInMethod CheckInMethod,
	Category? Category,
	List<string> Tags)
	: ICommand<bool>;
