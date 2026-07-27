using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Reports.CreateReport.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Reports.CreateReport;

public class CreateReportCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Organization, OrganizationId> _organizationRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateReportCommandHandler _sut;

	private static readonly UserId DefaultReporterId = UserId.New();

	public CreateReportCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Organizations.Returns(_organizationRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_sut = new CreateReportCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			OrganizationId.New(), "Titel", "Beschreibung", true, null, Occurrence.OneTime,
			ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldCreateReport_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var command = new CreateReportCommand(
			opportunity.Id.Value, ReportedContentType.VolunteerOpportunity, DefaultReporterId, ReportReason.Spam, null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().NotBe(Guid.Empty);
		await _reportRepo.Received(1).AddAsync(
			Arg.Is<Report>(r => r!.ContentId == opportunity.Id.Value && r.ContentType == ReportedContentType.VolunteerOpportunity),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCreateReport_WhenOrganizationExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organization = Organization.Create(OrganizationId.New(), "Test Org").Value;
		_organizationRepo.FindAsync(organization.Id, cancellationToken).Returns(organization);
		var command = new CreateReportCommand(
			organization.Id.Value, ReportedContentType.Organization, DefaultReporterId, ReportReason.Fraud, null);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().NotBe(Guid.Empty);
		await _reportRepo.Received(1).AddAsync(
			Arg.Is<Report>(r => r!.ContentId == organization.Id.Value && r.ContentType == ReportedContentType.Organization),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var contentId = Guid.NewGuid();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(contentId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);
		var command = new CreateReportCommand(contentId, ReportedContentType.VolunteerOpportunity, DefaultReporterId, ReportReason.Spam, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReasonIsOtherAndDetailIsMissing(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var command = new CreateReportCommand(
			opportunity.Id.Value, ReportedContentType.VolunteerOpportunity, DefaultReporterId, ReportReason.Other, null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Other*");
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}
}
