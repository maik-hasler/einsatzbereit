using Application.Common.Messaging;
using Domain.Reports;
using Domain.Users;

namespace Application.VolunteerOpportunities.ReportVolunteerOpportunity.v1;

public sealed record ReportVolunteerOpportunityCommand(
	Guid OpportunityId,
	UserId ReporterId,
	ReportReason Reason,
	string? Details)
	: ICommand<bool>;
