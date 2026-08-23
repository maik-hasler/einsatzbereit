using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.ReportOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.ReportOrganization;

public class ReportOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _organizationRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly ReportOrganizationCommandHandler _sut;

	private static readonly UserId DefaultReporterId = UserId.New();

	public ReportOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_organizationRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext
			.HasOpenReportAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		_sut = new ReportOrganizationCommandHandler(_dbContext);
	}

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(OrganizationId.Create(id).GetValueOrThrow(), "Test Org").Value;

	[Test]
	public async Task Handle_ShouldAddReport_WhenOrganizationExists(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo
			.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken)
			.Returns(organization);

		var command = new ReportOrganizationCommand(orgId, DefaultReporterId, ReportReason.Fraud, "fake org");

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		await _reportRepo.Received(1).AddAsync(
			Arg.Is<Report>(r => r!.TargetType == ReportTargetType.Organization
				&& r.TargetId == orgId
				&& r.ReporterId == DefaultReporterId
				&& r.Reason == ReportReason.Fraud
				&& r.Details == "fake org"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		_organizationRepo
			.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken)
			.Returns((Organization?)null);

		var command = new ReportOrganizationCommand(orgId, DefaultReporterId, ReportReason.Fraud, null);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenReporterAlreadyHasOpenReport(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo
			.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken)
			.Returns(organization);
		_dbContext
			.HasOpenReportAsync(ReportTargetType.Organization, orgId, DefaultReporterId, cancellationToken)
			.Returns(true);

		var command = new ReportOrganizationCommand(orgId, DefaultReporterId, ReportReason.Fraud, null);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		await _reportRepo.DidNotReceive().AddAsync(Arg.Any<Report>(), Arg.Any<CancellationToken>());
	}
}
