using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.SaveDashboardLayout.v1;

public sealed record DashboardWidgetPlacementInput(string WidgetKey, int X, int Y, int Width, int Height);

public sealed record SaveDashboardLayoutCommand(
	Guid OrganizationId,
	UserId RequestingUserId,
	IReadOnlyList<DashboardWidgetPlacementInput> Widgets)
	: ICommand<bool>;
