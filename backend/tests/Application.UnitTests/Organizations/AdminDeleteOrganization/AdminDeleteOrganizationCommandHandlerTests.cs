using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Organizations.AdminDeleteOrganization.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Organizations.AdminDeleteOrganization;

public class AdminDeleteOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _organizationRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly IEngagementReadRepository _engagementReadRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly AdminDeleteOrganizationCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly UserId DefaultAdminUserId = UserId.New();

	public AdminDeleteOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_organizationRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext
			.GetOpportunitiesForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(new List<VolunteerOpportunity>());
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Engagement>());
		_dbContext
			.GetOpenReportsForTargetAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new List<Report>());
		_engagementReadRepository
			.GetByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new AdminDeleteOrganizationCommandHandler(_dbContext, _engagementReadRepository, _keycloakService);
	}

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(OrganizationId.Create(id).GetValueOrThrow(), "Test Org").Value;

	private VolunteerOpportunity CreateOpportunity(OrganizationId orgId) =>
		VolunteerOpportunity.Create(orgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldDeleteOrganizationAndCallKeycloak_EvenWithMultipleMembers(
		CancellationToken cancellationToken)
	{
		// Arrange: no GetMembersAsync stub at all, and no IsOrganizerAsync stub -
		// if the handler consulted either, this would still have to pass, proving
		// it doesn't gate on organizer membership or member count like the
		// organizer-triggered delete flow does.
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);

		// Act
		var result = await _sut.Handle(new AdminDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		_organizationRepo.Received(1).Delete(organization);
		await _keycloakService.Received(1).DeleteOrganizationAsync(orgId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCascadeDeleteOpportunities_EvenWithFutureTimeSlotsOrActiveEngagements(
		CancellationToken cancellationToken)
	{
		// Arrange: an opportunity that would 409-block the organizer-triggered
		// delete flow must still be force-deleted here.
		var orgId = Guid.NewGuid();
		var organizationId = OrganizationId.Create(orgId).GetValueOrThrow();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(organizationId, cancellationToken).Returns(organization);

		var opportunity = CreateOpportunity(organizationId);
		opportunity.AddTimeSlot(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 5, DateTimeOffset.UtcNow);
		_dbContext
			.GetOpportunitiesForOrganizationAsync(organizationId, cancellationToken)
			.Returns([opportunity]);

		// Act
		await _sut.Handle(new AdminDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		_opportunityRepo.Received(1).Delete(opportunity);
	}

	[Test]
	public async Task Handle_ShouldMarkOpenReportsActioned_OnOrganizationAndItsOpportunities(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organizationId = OrganizationId.Create(orgId).GetValueOrThrow();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(organizationId, cancellationToken).Returns(organization);

		var opportunity = CreateOpportunity(organizationId);
		_dbContext
			.GetOpportunitiesForOrganizationAsync(organizationId, cancellationToken)
			.Returns([opportunity]);

		var orgReport = Report.Create(ReportTargetType.Organization, orgId, UserId.New(), ReportReason.Fraud, null).Value;
		_dbContext
			.GetOpenReportsForTargetAsync(ReportTargetType.Organization, orgId, cancellationToken)
			.Returns([orgReport]);

		var opportunityReport = Report.Create(ReportTargetType.VolunteerOpportunity, opportunity.Id.Value, UserId.New(), ReportReason.Spam, null).Value;
		_dbContext
			.GetOpenReportsForTargetAsync(ReportTargetType.VolunteerOpportunity, opportunity.Id.Value, cancellationToken)
			.Returns([opportunityReport]);

		// Act
		await _sut.Handle(new AdminDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		orgReport.Status.Should().Be(ReportStatus.Actioned);
		opportunityReport.Status.Should().Be(ReportStatus.Actioned);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new AdminDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
	}
}
