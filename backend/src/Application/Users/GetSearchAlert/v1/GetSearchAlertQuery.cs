using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.GetSearchAlert.v1;

public sealed record GetSearchAlertQuery(UserId UserId)
	: IQuery<SearchAlertResponse>;

public sealed record SearchAlertResponse(
	bool HasActiveAlert,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	IReadOnlyList<string> Categories,
	string? Tag);
