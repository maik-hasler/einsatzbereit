using Application.Engagements.CheckInEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Application.UnitTests.Engagements.CheckInEngagement;

public class EngagementCheckedInAuditLogHandlerTests
{
	private readonly FakeLogger<EngagementCheckedInAuditLogHandler> _logger = new();
	private readonly EngagementCheckedInAuditLogHandler _sut;

	public EngagementCheckedInAuditLogHandlerTests()
	{
		_sut = new EngagementCheckedInAuditLogHandler(_logger);
	}

	[Test]
	public async Task Handle_ShouldWriteAuditLogEntry_WithEngagementVolunteerAndOpportunityIds(
		CancellationToken cancellationToken)
	{
		var domainEvent = new EngagementCheckedInDomainEvent(
			EngagementId.New(),
			UserId.New(),
			VolunteerOpportunityId.New());

		await _sut.Handle(domainEvent, cancellationToken);

		var record = _logger.Collector.GetSnapshot().Should().ContainSingle().Subject;

		record.Level.Should().Be(LogLevel.Information);
		record.Message.Should().Contain(domainEvent.VolunteerId.Value.ToString());
		record.Message.Should().Contain(domainEvent.EngagementId.Value.ToString());
		record.Message.Should().Contain(domainEvent.OpportunityId.Value.ToString());
	}
}
