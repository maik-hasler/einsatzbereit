using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.CancelVolunteerOpportunity.v1;

public sealed record CancelVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId RequestingUserId,
	string? Reason = null)
	: ICommand<bool>;
