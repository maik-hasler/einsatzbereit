using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.SaveSearchAlert.v1;

public sealed record SaveSearchAlertCommand(
	UserId UserId,
	string? Occurrence,
	string? ParticipationType,
	bool? IsRemote,
	double? CenterLatitude,
	double? CenterLongitude,
	double? RadiusKm,
	IReadOnlyList<string>? Categories,
	string? Tag)
	: ICommand<bool>;
