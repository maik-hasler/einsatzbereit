namespace Api.Users.SaveSearchAlert.v1;

public sealed record SaveSearchAlertRequest(
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	IReadOnlyList<string>? Categories,
	string? Tag);
