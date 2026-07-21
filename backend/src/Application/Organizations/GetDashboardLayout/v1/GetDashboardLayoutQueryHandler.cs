using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.GetDashboardLayout.v1;

internal sealed class GetDashboardLayoutQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetDashboardLayoutQuery, DashboardLayoutResponse>
{
	public async ValueTask<DashboardLayoutResponse> Handle(
		GetDashboardLayoutQuery request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		_ = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var layout = await dbContext.GetDashboardLayoutAsync(
			organizationId, request.RequestingUserId, cancellationToken);

		var widgets = layout?.Widgets
			.Select(w => new DashboardWidgetPlacementResponse(w.WidgetKey.ToString(), w.X, w.Y, w.Width, w.Height))
			.ToList() ?? [];

		return new DashboardLayoutResponse(layout is not null, widgets);
	}
}
