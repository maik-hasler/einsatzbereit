using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

internal sealed class CreateTimeSlotCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CreateTimeSlotCommand, IReadOnlyList<TimeSlot>>
{
	private const int MaxRecurrenceCount = 52;

	public async ValueTask<IReadOnlyList<TimeSlot>> Handle(
		CreateTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var count = Math.Clamp(request.RecurrenceCount, 1, MaxRecurrenceCount);
		var duration = request.EndDateTime - request.StartDateTime;
		var slots = new List<TimeSlot>(count);
		var now = DateTimeOffset.UtcNow;

		for (var i = 0; i < count; i++)
		{
			var start = Advance(request.StartDateTime, request.RecurrenceFrequency, i);
			var end = start + duration;
			slots.Add(opportunity.AddTimeSlot(start, end, request.MaxParticipants, now).GetValueOrThrow());
		}

		return slots;
	}

	private static DateTimeOffset Advance(DateTimeOffset origin, string? frequency, int steps) =>
		frequency?.ToUpperInvariant() switch
		{
			"WEEKLY" => origin.AddDays(7 * steps),
			"MONTHLY" => origin.AddMonths(steps),
			_ => origin
		};
}
