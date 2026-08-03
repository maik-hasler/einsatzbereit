using Application.Common.Messaging;

namespace Application.Engagements.GetMyEngagementRecord.v1;

internal sealed class GetMyEngagementRecordQueryHandler(
	IEngagementReadRepository readRepository)
	: IQueryHandler<GetMyEngagementRecordQuery, List<EngagementRecordEntry>>
{
	public async ValueTask<List<EngagementRecordEntry>> Handle(
		GetMyEngagementRecordQuery request,
		CancellationToken cancellationToken = default)
	{
		var engagements = await readRepository.GetCheckedInByVolunteerAsync(request.UserId, cancellationToken);

		// An IndividualContact engagement has no time slot at all, so there is no
		// duration to print for it - exclude rather than show a blank/zero one.
		return engagements
			.Where(e => e.TimeSlotStartDateTime is not null && e.TimeSlotEndDateTime is not null)
			.Select(e => new EngagementRecordEntry(
				e.Id,
				e.OpportunityTitle,
				e.OrganizationName,
				e.TimeSlotStartDateTime!.Value,
				e.TimeSlotEndDateTime!.Value,
				(e.TimeSlotEndDateTime!.Value - e.TimeSlotStartDateTime!.Value).TotalHours))
			.OrderByDescending(e => e.StartDateTime)
			.ToList();
	}
}
