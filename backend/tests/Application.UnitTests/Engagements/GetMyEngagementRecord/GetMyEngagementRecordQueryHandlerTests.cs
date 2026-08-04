using Application.Engagements;
using Application.Engagements.GetMyEngagementRecord.v1;
using AwesomeAssertions;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetMyEngagementRecord;

public class GetMyEngagementRecordQueryHandlerTests
{
	private readonly IEngagementReadRepository _readRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly GetMyEngagementRecordQueryHandler _sut;
	private readonly UserId _volunteerId = UserId.New();

	public GetMyEngagementRecordQueryHandlerTests()
	{
		_sut = new GetMyEngagementRecordQueryHandler(_readRepository);
	}

	private static EngagementSummary CheckedInSummary(
		string? opportunityTitle = "Beach Cleanup",
		string? organizationName = "Green Org",
		DateTimeOffset? start = null,
		DateTimeOffset? end = null) =>
		new(
			Guid.NewGuid(),
			Guid.NewGuid(),
			opportunityTitle,
			Guid.NewGuid(),
			organizationName,
			Guid.NewGuid(),
			Guid.NewGuid(),
			Message: null,
			Status: "Confirmed",
			IsCheckedIn: true,
			HasFeedback: false,
			CreatedOn: DateTimeOffset.UtcNow.AddDays(-10),
			TimeSlotStartDateTime: start,
			TimeSlotEndDateTime: end);

	[Test]
	public async Task Handle_ShouldReturnEntry_ForCheckedInEngagementWithTimeSlot()
	{
		var start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
		var end = start.AddHours(3);
		var summary = CheckedInSummary(start: start, end: end);
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([summary]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().ContainSingle();
		var entry = result[0];
		entry.EngagementId.Should().Be(summary.Id);
		entry.OpportunityTitle.Should().Be(summary.OpportunityTitle);
		entry.OrganizationName.Should().Be(summary.OrganizationName);
		entry.StartDateTime.Should().Be(start);
		entry.EndDateTime.Should().Be(end);
		entry.Hours.Should().Be(3);
	}

	[Test]
	public async Task Handle_ShouldComputeFractionalHours()
	{
		var start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
		var end = start.AddMinutes(90);
		var summary = CheckedInSummary(start: start, end: end);
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([summary]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().ContainSingle();
		result[0].Hours.Should().Be(1.5);
	}

	[Test]
	public async Task Handle_ShouldExcludeEntry_WhenNoTimeSlotDatesAvailable()
	{
		// An IndividualContact engagement has no time slot at all, so there is
		// nothing to derive a duration from.
		var summary = CheckedInSummary(start: null, end: null);
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([summary]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyList_WhenNoCheckedInEngagements()
	{
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldOrderEntries_ByStartDateTimeDescending()
	{
		var earliest = CheckedInSummary(
			opportunityTitle: "Earliest",
			start: new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero),
			end: new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
		var latest = CheckedInSummary(
			opportunityTitle: "Latest",
			start: new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
			end: new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero));
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([earliest, latest]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().HaveCount(2);
		result[0].OpportunityTitle.Should().Be("Latest");
		result[1].OpportunityTitle.Should().Be("Earliest");
	}

	[Test]
	public async Task Handle_ShouldKeepEntry_WhenOrganizationNameIsNull()
	{
		// Opportunity/organization may have been hard-deleted since the check-in;
		// the entry itself should still be kept (just without an org name).
		var start = new DateTimeOffset(2026, 1, 10, 9, 0, 0, TimeSpan.Zero);
		var summary = CheckedInSummary(organizationName: null, start: start, end: start.AddHours(2));
		_readRepository
			.GetCheckedInByVolunteerAsync(_volunteerId, Arg.Any<CancellationToken>())
			.Returns([summary]);

		var result = await _sut.Handle(new GetMyEngagementRecordQuery(_volunteerId), CancellationToken.None);

		result.Should().ContainSingle();
		result[0].OrganizationName.Should().BeNull();
	}
}
