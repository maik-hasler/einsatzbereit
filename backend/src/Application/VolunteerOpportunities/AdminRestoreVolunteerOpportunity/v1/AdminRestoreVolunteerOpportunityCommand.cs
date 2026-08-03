using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.AdminRestoreVolunteerOpportunity.v1;

public sealed record AdminRestoreVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId AdminUserId)
	: ICommand<bool>;
