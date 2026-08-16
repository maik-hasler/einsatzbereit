namespace Application.Organizations.GetOrganizationCalendarEvents.v1;

public sealed record OrganizationCalendarEventDto(
	Guid OpportunityId,
	string TitleDe,
	string? TitleEn,
	string? Color,
	IReadOnlyList<CalendarTimeSlotDto> TimeSlots);

public sealed record CalendarTimeSlotDto(
	Guid TimeSlotId,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	int? MaxParticipants,
	int BookedCount);
