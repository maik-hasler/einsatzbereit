using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.SearchAlerts;
using Domain.VolunteerOpportunities;

namespace Application.Users.SaveSearchAlert.v1;

internal sealed class SaveSearchAlertCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<SaveSearchAlertCommand, bool>
{
	public async ValueTask<bool> Handle(
		SaveSearchAlertCommand request,
		CancellationToken cancellationToken = default)
	{
		var occurrence = ParseOccurrence(request.Occurrence);
		var participationType = ParseParticipationType(request.ParticipationType);
		var categories = ParseCategories(request.Categories);

		ValidateRadius(request.CenterLatitude, request.CenterLongitude, request.RadiusKm);

		var alert = await dbContext.GetSearchAlertForUserAsync(request.UserId, cancellationToken);

		if (alert is null)
		{
			alert = SearchAlert.Create(
				request.UserId,
				occurrence,
				participationType,
				request.IsRemote,
				request.CenterLatitude,
				request.CenterLongitude,
				request.RadiusKm,
				categories,
				request.Tag);
			await dbContext.SearchAlerts.AddAsync(alert, cancellationToken);
		}
		else
		{
			alert.ReplaceCriteria(
				occurrence,
				participationType,
				request.IsRemote,
				request.CenterLatitude,
				request.CenterLongitude,
				request.RadiusKm,
				categories,
				request.Tag);
		}

		return true;
	}

	private static Occurrence? ParseOccurrence(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (!Enum.TryParse<Occurrence>(value, ignoreCase: true, out var occurrence) || !Enum.IsDefined(occurrence))
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.InvalidOccurrence", $"Unknown occurrence '{value}'."));

		return occurrence;
	}

	private static ParticipationType? ParseParticipationType(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;

		if (!Enum.TryParse<ParticipationType>(value, ignoreCase: true, out var participationType) || !Enum.IsDefined(participationType))
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.InvalidParticipationType", $"Unknown participation type '{value}'."));

		return participationType;
	}

	private static List<string> ParseCategories(IReadOnlyList<string>? values)
	{
		if (values is null or { Count: 0 })
			return [];

		var categories = new List<string>();

		foreach (var value in values)
		{
			if (!Enum.TryParse<Category>(value, ignoreCase: true, out var category) || !Enum.IsDefined(category))
				throw new ResultFailureException(Error.Validation(
					"SearchAlert.InvalidCategory", $"Unknown category '{value}'."));

			categories.Add(category.ToString());
		}

		return categories;
	}

	private static void ValidateRadius(double? centerLatitude, double? centerLongitude, double? radiusKm)
	{
		var hasLat = centerLatitude is not null;
		var hasLng = centerLongitude is not null;

		if (hasLat != hasLng)
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.IncompleteLocation", "CenterLatitude and CenterLongitude must both be supplied together."));

		if (centerLatitude is < -90 or > 90)
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.InvalidLatitude", "CenterLatitude must be between -90 and 90."));

		if (centerLongitude is < -180 or > 180)
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.InvalidLongitude", "CenterLongitude must be between -180 and 180."));

		if (radiusKm is <= 0 or > 500)
			throw new ResultFailureException(Error.Validation(
				"SearchAlert.InvalidRadius", "RadiusKm must be between 1 and 500."));
	}
}
