using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Organizations.DeleteOrganization.v1;
using Application.Reports.ResolveReport.v1;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using NSubstitute;
using NSubstitute.Core;

namespace Application.UnitTests.Reports.ResolveReport;

public class ResolveReportCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly ResolveReportCommandHandler _sut;

	private static readonly UserId DefaultActingUserId = UserId.New();

	public ResolveReportCommandHandlerTests()
	{
		_dbContext.Reports.Returns(_reportRepo);
		_sut = new ResolveReportCommandHandler(_dbContext, _sender);
	}

	[Test]
	public async Task Handle_ShouldDeleteOpportunityAsAdmin_AndResolveReport_WhenContentIsAnOpportunity(
		CancellationToken cancellationToken)
	{
		// Arrange
		var contentId = Guid.NewGuid();
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, contentId, UserId.New(), ReportReason.Spam, null).Value;
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);

		// Act
		var result = await _sut.Handle(new ResolveReportCommand(report.Id.Value, DefaultActingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Resolved);
		await _sender.Received(1).Send(
			Arg.Is<DeleteVolunteerOpportunityCommand>(c => c!.OpportunityId == contentId && c.RequestingUserId == DefaultActingUserId && c.IsAdmin),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldDeleteOrganizationAsAdmin_AndResolveReport_WhenContentIsAnOrganization(
		CancellationToken cancellationToken)
	{
		// Arrange
		var contentId = Guid.NewGuid();
		var report = Report.Create(ReportedContentType.Organization, contentId, UserId.New(), ReportReason.Fraud, null).Value;
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);

		// Act
		var result = await _sut.Handle(new ResolveReportCommand(report.Id.Value, DefaultActingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Resolved);
		await _sender.Received(1).Send(
			Arg.Is<DeleteOrganizationCommand>(c => c!.OrganizationId == contentId && c.RequestingUserId == DefaultActingUserId && c.IsAdmin),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldStillResolveReport_WhenContentWasAlreadyDeleted(
		CancellationToken cancellationToken)
	{
		// Arrange: e.g. the organizer deleted the opportunity themselves before the admin acted.
		var contentId = Guid.NewGuid();
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, contentId, UserId.New(), ReportReason.Spam, null).Value;
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);
		_sender
			.Send(Arg.Any<DeleteVolunteerOpportunityCommand>(), Arg.Any<CancellationToken>())
			.Returns(ThrowNotFound);

		// Act
		var result = await _sut.Handle(new ResolveReportCommand(report.Id.Value, DefaultActingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Resolved);
	}

	[Test]
	public async Task Handle_ShouldPropagateConflict_AndLeaveReportPending_WhenDeleteFailsWithConflict(
		CancellationToken cancellationToken)
	{
		// Arrange: e.g. the organization still has other members - a real conflict, not "already gone".
		var contentId = Guid.NewGuid();
		var report = Report.Create(ReportedContentType.Organization, contentId, UserId.New(), ReportReason.Spam, null).Value;
		_reportRepo.FindAsync(report.Id, cancellationToken).Returns(report);
		_sender
			.Send(Arg.Any<DeleteOrganizationCommand>(), Arg.Any<CancellationToken>())
			.Returns(ThrowConflict);

		// Act
		Func<Task> act = async () => await _sut.Handle(new ResolveReportCommand(report.Id.Value, DefaultActingUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		report.Status.Should().Be(ReportStatus.Pending);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReportNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var reportId = Guid.NewGuid();
		_reportRepo.FindAsync(ReportId.Create(reportId).GetValueOrThrow(), cancellationToken).Returns((Report?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new ResolveReportCommand(reportId, DefaultActingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{reportId}*");
	}

	private static bool ThrowNotFound(CallInfo _) =>
		throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", "not found"));

	private static bool ThrowConflict(CallInfo _) =>
		throw new ResultFailureException(Error.Conflict("Organization.MultipleMembers", "conflict"));
}
