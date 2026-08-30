using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.ResetDashboardLayout.v1;

public sealed record ResetDashboardLayoutCommand(
	Guid OrganizationId,
	UserId RequestingUserId)
	: ICommand<bool>;
