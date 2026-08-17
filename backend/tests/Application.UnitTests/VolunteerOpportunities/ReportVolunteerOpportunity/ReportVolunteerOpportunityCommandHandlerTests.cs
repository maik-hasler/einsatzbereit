using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.ReportVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.ReportVolunteerOpportunity;

public class ReportVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly ReportVolunteerOpportunityCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultReporterId = UserId.New();

	public ReportVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext
			.HasOpenReportAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_sut = new ReportVolunteerOpportunityCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		Domain.VolunteerOpportunities.VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldAddReport_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new ReportVolunteerOpportunityCommand(opportunityId, DefaultReporterId, ReportReason.Spam, "looks fake");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		await _reportRepo.Received(1).AddAsync(
			Arg.Is<Report>(r => r!.TargetType == ReportTargetType.VolunteerOpportunity
				&& r.TargetId == opportunityId
				&& r.ReporterId == DefaultReporterId
				&& r.Reason == ReportReason.Spam
				&& r.Details == "looks fake"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		var command = new ReportVolunteerOpportunityCommand(opportunityId, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReporterAlreadyHasOpenReport(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.HasOpenReportAsync(ReportTargetType.VolunteerOpportunity, opportunityId, DefaultReporterId, cancellationToken)
			.Returns(true);

		var command = new ReportVolunteerOpportunityCommand(opportunityId, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenDetailsTooLong(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var tooLong = new string('a', Domain.Reports.Report.MaxDetailsLength + 1);
		var command = new ReportVolunteerOpportunityCommand(opportunityId, DefaultReporterId, ReportReason.Other, tooLong);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Validation);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}
}
