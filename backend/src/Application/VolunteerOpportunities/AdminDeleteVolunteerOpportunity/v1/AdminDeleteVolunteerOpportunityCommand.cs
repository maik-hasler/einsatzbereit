using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.AdminDeleteVolunteerOpportunity.v1;

public sealed record AdminDeleteVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId AdminUserId)
	: ICommand<bool>;
