using Application.Common.Messaging;
using Domain.Users;

namespace Application.VolunteerOpportunities.AdminShadowDeleteVolunteerOpportunity.v1;

public sealed record AdminShadowDeleteVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId AdminUserId)
	: ICommand<bool>;
