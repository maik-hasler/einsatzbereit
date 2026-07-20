using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.SaveDashboardLayout.v1;

internal sealed class SaveDashboardLayoutCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<SaveDashboardLayoutCommand, bool>
{
	public async ValueTask<bool> Handle(
		SaveDashboardLayoutCommand request,
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

		var widgets = ParseWidgets(request.Widgets);

		var layout = await dbContext.GetDashboardLayoutAsync(
			organizationId, request.RequestingUserId, cancellationToken);

		if (layout is null)
		{
			layout = OrganizationDashboardLayout.Create(organizationId, request.RequestingUserId, widgets);
			await dbContext.OrganizationDashboardLayouts.AddAsync(layout, cancellationToken);
		}
		else
		{
			layout.ReplaceWidgets(widgets);
		}

		return true;
	}

	private static List<DashboardWidgetPlacement> ParseWidgets(
		IReadOnlyList<DashboardWidgetPlacementInput> inputs)
	{
		var widgets = new List<DashboardWidgetPlacement>();
		var seenKeys = new HashSet<DashboardWidgetKey>();

		foreach (var input in inputs)
		{
			if (!Enum.TryParse<DashboardWidgetKey>(input.WidgetKey, out var key) || !Enum.IsDefined(key))
				throw new ResultFailureException(Error.Validation(
					"DashboardLayout.InvalidWidgetKey", $"Unknown widget key '{input.WidgetKey}'."));

			if (!seenKeys.Add(key))
				throw new ResultFailureException(Error.Validation(
					"DashboardLayout.DuplicateWidget", $"Widget '{key}' is placed more than once."));

			widgets.Add(new DashboardWidgetPlacement(key));
		}

		return widgets;
	}
}
