using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetVolunteerOpportunityDateAvailability;

public class GetVolunteerOpportunityDateAvailabilityQueryHandlerTests
{
	private static readonly DateTimeOffset From = new(2026, 8, 1, 0, 0, 0, TimeSpan.FromHours(2));
	private static readonly DateTimeOffset To = new(2026, 8, 31, 23, 59, 59, TimeSpan.FromHours(2));

	private readonly IVolunteerOpportunityReadRepository _readRepo =
		Substitute.For<IVolunteerOpportunityReadRepository>();
	private readonly GetVolunteerOpportunityDateAvailabilityQueryHandler _sut;

	public GetVolunteerOpportunityDateAvailabilityQueryHandlerTests()
	{
		_readRepo
			.GetDateAvailabilityAsync(Arg.Any<VolunteerOpportunityDateAvailabilityFilter>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new GetVolunteerOpportunityDateAvailabilityQueryHandler(_readRepo);
	}

	private static GetVolunteerOpportunityDateAvailabilityQuery Query() =>
		new(
			From,
			To,
			"America/New_York",
			"Recurring",
			"ScheduledSlots",
			true,
			52.5,
			13.4,
			25,
			["Environment"],
			"cleanup",
			"beach");

	private async Task<VolunteerOpportunityDateAvailabilityFilter> CapturedFilterAsync()
	{
		VolunteerOpportunityDateAvailabilityFilter? captured = null;
		_readRepo
			.GetDateAvailabilityAsync(
				Arg.Do<VolunteerOpportunityDateAvailabilityFilter>(f => captured = f),
				Arg.Any<CancellationToken>())
			.Returns([]);

		await _sut.Handle(Query(), CancellationToken.None);

		captured.Should().NotBeNull();
		return captured!;
	}

	[Test]
	public async Task Handle_ShouldForwardEveryFilter_ToTheReadRepository()
	{
		var filter = await CapturedFilterAsync();

		filter.From.Should().Be(From);
		filter.To.Should().Be(To);
		filter.Timezone.Should().Be("America/New_York");
		filter.Occurrence.Should().Be("Recurring");
		filter.ParticipationType.Should().Be("ScheduledSlots");
		filter.IsRemote.Should().BeTrue();
		filter.CenterLatitude.Should().Be(52.5);
		filter.CenterLongitude.Should().Be(13.4);
		filter.RadiusKm.Should().Be(25);
		filter.Categories.Should().Equal("Environment");
		filter.Tag.Should().Be("cleanup");
		filter.Keyword.Should().Be("beach");
	}

	[Test]
	public async Task Handle_ShouldReportRadiusFilteringIsWanted_WhenACompleteCenterAndRadiusAreGiven()
	{
		var filter = await CapturedFilterAsync();

		filter.HasRadius.Should().BeTrue();
	}

	[Test]
	public void Filter_ShouldNotReportRadiusFiltering_WhenTheRadiusIsMissing()
	{
		var filter = new VolunteerOpportunityDateAvailabilityFilter(From, To, null, CenterLatitude: 52.5, CenterLongitude: 13.4);

		filter.HasRadius.Should().BeFalse(
			"a center without a radius describes no circle to filter by");
	}

	[Test]
	public async Task Handle_ShouldReturnWhateverTheReadRepositoryAnswers()
	{
		IReadOnlyList<VolunteerOpportunityAvailableDate> days =
			[new("2026-08-13", 2), new("2026-08-20", 1)];
		_readRepo
			.GetDateAvailabilityAsync(Arg.Any<VolunteerOpportunityDateAvailabilityFilter>(), Arg.Any<CancellationToken>())
			.Returns(days);

		var result = await _sut.Handle(Query(), CancellationToken.None);

		result.Should().BeEquivalentTo(days);
	}
}
