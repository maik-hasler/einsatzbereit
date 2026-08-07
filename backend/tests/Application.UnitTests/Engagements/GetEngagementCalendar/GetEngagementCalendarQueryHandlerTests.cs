using Application.Common.Exceptions;
using Application.Engagements;
using Application.Engagements.GetEngagementCalendar.v1;
using AwesomeAssertions;
using Domain.Engagements;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetEngagementCalendar;

public class GetEngagementCalendarQueryHandlerTests
{
	private readonly IEngagementReadRepository _readRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly GetEngagementCalendarQueryHandler _sut;

	public GetEngagementCalendarQueryHandlerTests()
	{
		_sut = new GetEngagementCalendarQueryHandler(_readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenReadRepositoryFindsNothing(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns((EngagementCalendarInfo?)null);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldReturnIcsFile_WithExpectedFileNameAndCoreFields(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var opportunityId = Guid.CreateVersion7();
		var start = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
		var end = start.AddHours(2);
		var info = new EngagementCalendarInfo(
			engagementId,
			opportunityId,
			"Test Opportunity",
			"Some description",
			"Main St 1, 12345 Berlin",
			start,
			end);
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.FileName.Should().Be($"engagement-{engagementId}.ics");
		result.Content.Should().Contain("BEGIN:VCALENDAR");
		result.Content.Should().Contain("END:VCALENDAR");
		result.Content.Should().Contain($"UID:{engagementId}@einsatzbereit");
		result.Content.Should().Contain("DTSTART:20260801T100000Z");
		result.Content.Should().Contain("DTEND:20260801T120000Z");
		result.Content.Should().Contain("SUMMARY:Test Opportunity");
		result.Content.Should().Contain("DESCRIPTION:Some description");
		result.Content.Should().Contain("LOCATION:Main St 1\\, 12345 Berlin");
		result.Content.Should().Contain($"URL:https://einsatzbereit.example/volunteer-opportunities/{opportunityId}");
	}

	[Test]
	public async Task Handle_ShouldOmitDescriptionAndLocation_WhenBlank(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var info = new EngagementCalendarInfo(
			engagementId,
			Guid.CreateVersion7(),
			"Test Opportunity",
			"   ",
			null,
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2));
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Content.Should().NotContain("DESCRIPTION:");
		result.Content.Should().NotContain("LOCATION:");
	}

	[Test]
	[Arguments("back\\slash", "SUMMARY:back\\\\slash")]
	[Arguments("semi;colon", "SUMMARY:semi\\;colon")]
	[Arguments("com,ma", "SUMMARY:com\\,ma")]
	[Arguments("line\nbreak", "SUMMARY:line\\nbreak")]
	[Arguments("line\r\nbreak", "SUMMARY:line\\nbreak")]
	public async Task Handle_ShouldEscapeSpecialCharacters_InSummary(
		string rawTitle,
		string expectedSummaryLine,
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var info = new EngagementCalendarInfo(
			engagementId,
			Guid.CreateVersion7(),
			rawTitle,
			"Description",
			null,
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2));
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Content.Should().Contain(expectedSummaryLine);
	}

	[Test]
	public async Task Handle_ShouldFoldLinesLongerThan75Octets(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var longTitle = new string('A', 100);
		var info = new EngagementCalendarInfo(
			engagementId,
			Guid.CreateVersion7(),
			longTitle,
			"Description",
			null,
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2));
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Content.Should().Contain("\r\n ");
		result.Content.Should().Contain(longTitle[..25]);
	}

	// Regression for #1729: StringBuilder.AppendLine uses Environment.NewLine,
	// which is "\n" (not "\r\n") on Linux - mixing that with the folding logic's
	// explicit "\r\n" produced a file that violated RFC 5545's CRLF requirement.
	[Test]
	public async Task Handle_ShouldUseCrLf_ForEveryLineEnding(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var longTitle = new string('A', 100);
		var info = new EngagementCalendarInfo(
			engagementId,
			Guid.CreateVersion7(),
			longTitle,
			"Description",
			"Some Location",
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2));
		_readRepository
			.GetCalendarInfoAsync(EngagementId.Create(engagementId).GetValueOrThrow(), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarQuery(engagementId, "https://einsatzbereit.example");

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert - every "\n" in the file must be immediately preceded by "\r",
		// i.e. there is no bare LF anywhere once every CRLF is stripped out.
		result.Should().NotBeNull();
		result!.Content.Replace("\r\n", string.Empty).Should().NotContain("\n");
		result.Content.Replace("\r\n", string.Empty).Should().NotContain("\r");
	}
}
