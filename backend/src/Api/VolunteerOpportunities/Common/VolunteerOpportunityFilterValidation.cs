using Domain.VolunteerOpportunities;

namespace Api.VolunteerOpportunities.Common;

// The public listing and the date-availability calendar behind its date filter take the
// same filter vocabulary, so they have to reject the same bad input the same way: a
// category typo that 400s on /volunteer-opportunities but is quietly dropped by the
// calendar would mark days the list then can't fill (#1779).
internal static class VolunteerOpportunityFilterValidation
{
	/// <summary>
	/// Returns a problem result for the first rule the shared filter parameters break,
	/// or null when all of them are valid.
	/// </summary>
	public static IResult? Validate(
		double? centerLatitude,
		double? centerLongitude,
		double? radiusKm,
		string? occurrence,
		string? participationType,
		string[]? categories,
		string? keyword)
	{
		var hasLat = centerLatitude.HasValue;
		var hasLng = centerLongitude.HasValue;
		if (hasLat != hasLng)
			return Results.Problem("CenterLatitude and CenterLongitude must both be supplied together.", statusCode: StatusCodes.Status400BadRequest);

		if (centerLatitude is < -90 or > 90)
			return Results.Problem("CenterLatitude must be between -90 and 90.", statusCode: StatusCodes.Status400BadRequest);

		if (centerLongitude is < -180 or > 180)
			return Results.Problem("CenterLongitude must be between -180 and 180.", statusCode: StatusCodes.Status400BadRequest);

		if (radiusKm is <= 0 or > 500)
			return Results.Problem("RadiusKm must be between 1 and 500.", statusCode: StatusCodes.Status400BadRequest);

		if (!string.IsNullOrWhiteSpace(occurrence)
			&& (!Enum.TryParse<Occurrence>(occurrence, ignoreCase: true, out var parsedOccurrence) || !Enum.IsDefined(parsedOccurrence)))
		{
			return Results.Problem(
				"Invalid occurrence. Allowed values: OneTime, Recurring.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (!string.IsNullOrWhiteSpace(participationType)
			&& (!Enum.TryParse<ParticipationType>(participationType, ignoreCase: true, out var parsedParticipationType) || !Enum.IsDefined(parsedParticipationType)))
		{
			return Results.Problem(
				"Invalid participation type. Allowed values: ScheduledSlots, IndividualContact.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (categories is { Length: > 0 }
			&& categories.Any(c => !Enum.TryParse<Category>(c, ignoreCase: true, out var category) || !Enum.IsDefined(category)))
		{
			return Results.Problem(
				"Invalid category. Allowed values: Social, Environment, Sport, Education, DisasterRelief, Health, Animals, Culture, Technology, Other.",
				statusCode: StatusCodes.Status400BadRequest);
		}

		if (keyword is { Length: > 200 })
			return Results.Problem("Keyword must be at most 200 characters.", statusCode: StatusCodes.Status400BadRequest);

		return null;
	}
}
