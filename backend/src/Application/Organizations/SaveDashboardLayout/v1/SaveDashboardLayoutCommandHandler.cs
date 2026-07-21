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

			if (input.Width < 1 || input.Height < 1 || input.X < 1 || input.Y < 1
				|| input.X + input.Width - 1 > DashboardGrid.Columns)
				throw new ResultFailureException(Error.Validation(
					"DashboardLayout.InvalidPlacement",
					$"Widget '{key}' has an invalid grid placement (x={input.X}, y={input.Y}, width={input.Width}, height={input.Height})."));

			widgets.Add(new DashboardWidgetPlacement(key, input.X, input.Y, input.Width, input.Height));
		}

		for (var i = 0; i < widgets.Count; i++)
		{
			for (var j = i + 1; j < widgets.Count; j++)
			{
				if (Overlaps(widgets[i], widgets[j]))
					throw new ResultFailureException(Error.Validation(
						"DashboardLayout.OverlappingPlacement",
						$"Widgets '{widgets[i].WidgetKey}' and '{widgets[j].WidgetKey}' overlap on the grid."));
			}
		}

		return widgets;
	}

	private static bool Overlaps(DashboardWidgetPlacement a, DashboardWidgetPlacement b) =>
		a.X < b.X + b.Width && b.X < a.X + a.Width &&
		a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
