using Application.Engagements.CheckInEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Application.UnitTests.Engagements.CheckInEngagement;

public class EngagementCheckedInAuditLogHandlerTests
{
	private readonly EngagementCheckedInAuditLogHandler _sut =
		new(NullLogger<EngagementCheckedInAuditLogHandler>.Instance);

	[Test]
	public async Task Handle_ShouldNotThrow_WhenEngagementCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var domainEvent = new EngagementCheckedInDomainEvent(
			EngagementId.New(),
			UserId.New(),
			VolunteerOpportunityId.New());

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
	}
}
