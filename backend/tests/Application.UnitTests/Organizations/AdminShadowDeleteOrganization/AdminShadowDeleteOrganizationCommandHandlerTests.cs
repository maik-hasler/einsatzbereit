using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Organizations.AdminShadowDeleteOrganization.v1;
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

namespace Application.UnitTests.Organizations.AdminShadowDeleteOrganization;

public class AdminShadowDeleteOrganizationCommandHandlerTests
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
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly AdminShadowDeleteOrganizationCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly UserId DefaultAdminUserId = UserId.New();

	public AdminShadowDeleteOrganizationCommandHandlerTests()
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
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_sut = new AdminShadowDeleteOrganizationCommandHandler(_dbContext, _engagementReadRepository, _keycloakUserService, _emailService, _emailTemplateRenderer);
	}

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(OrganizationId.Create(id).GetValueOrThrow(), "Test Org").Value;

	private VolunteerOpportunity CreateOpportunity(OrganizationId orgId) =>
		VolunteerOpportunity.Create(orgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldShadowDeleteOrganization_EvenWithMultipleMembers(
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
		var result = await _sut.Handle(new AdminShadowDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert: shadow-deleted, not hard-deleted or removed from Keycloak - the
		// takedown must be restorable.
		result.Should().BeTrue();
		organization.IsDeleted.Should().BeTrue();
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
	}

	[Test]
	public async Task Handle_ShouldCascadeShadowDeleteOpportunities_EvenWithFutureTimeSlotsOrActiveEngagements(
		CancellationToken cancellationToken)
	{
		// Arrange: an opportunity that would 409-block the organizer-triggered
		// delete flow must still be force-shadow-deleted here.
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
		await _sut.Handle(new AdminShadowDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		opportunity.IsDeleted.Should().BeTrue();
		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
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
		await _sut.Handle(new AdminShadowDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		orgReport.Status.Should().Be(ReportStatus.Actioned);
		opportunityReport.Status.Should().Be(ReportStatus.Actioned);
	}

	[Test]
	public async Task Handle_ShouldEmailEngagedVolunteers_AcrossAllCascadedOpportunities(
		CancellationToken cancellationToken)
	{
		// Arrange - the cascade must email affected volunteers on every one of the
		// org's opportunities (#1057), not just the first.
		var orgId = Guid.NewGuid();
		var organizationId = OrganizationId.Create(orgId).GetValueOrThrow();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(organizationId, cancellationToken).Returns(organization);

		var timeSlotId = TimeSlotId.New();
		var opportunityA = CreateOpportunity(organizationId);
		var engagementA = Engagement.CreateSlotSignUp(opportunityA.Id, UserId.New(), timeSlotId);
		engagementA.Confirm();
		var opportunityB = CreateOpportunity(organizationId);
		var engagementB = Engagement.CreateSlotSignUp(opportunityB.Id, UserId.New(), timeSlotId);
		engagementB.Confirm();

		_dbContext
			.GetOpportunitiesForOrganizationAsync(organizationId, cancellationToken)
			.Returns([opportunityA, opportunityB]);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(opportunityA.Id, cancellationToken)
			.Returns([engagementA]);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(opportunityB.Id, cancellationToken)
			.Returns([engagementB]);

		// Act
		await _sut.Handle(new AdminShadowDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		await _emailService.Received(2).SendAsync(
			"user@example.com",
			"Test Subject",
			"Test Body",
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new AdminShadowDeleteOrganizationCommand(orgId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}
}
