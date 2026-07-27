using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.DeleteOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Organizations.DeleteOrganization;

public class DeleteOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _organizationRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly DeleteOrganizationCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public DeleteOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_organizationRepo);
		_dbContext
			.GetBlockingOpportunitiesForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(new List<VolunteerOpportunity>());
		_sut = new DeleteOrganizationCommandHandler(_dbContext, _keycloakService);
	}

	private void AllowRequestingUserInOrg(Guid orgId) =>
		_dbContext
			.IsOrganizerAsync(OrganizationId.Create(orgId).GetValueOrThrow(), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(true);

	private void SetMembers(Guid orgId, params Guid[] memberIds) =>
		_keycloakService
			.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)memberIds
				.Select(id => new KeycloakOrganizationMember(id, "user", "First", "Last", "user@example.com", false))
				.ToList());

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(OrganizationId.Create(id).GetValueOrThrow(), "Test Org").Value;

	private VolunteerOpportunity CreateOpportunityWithFutureTimeSlot(OrganizationId orgId)
	{
		var opportunity = VolunteerOpportunity.Create(
			orgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 5, DateTimeOffset.UtcNow);
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldDeleteOrganizationAndCallKeycloak_WhenSoleMemberAndNoBlockingOpportunities(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_organizationRepo.Received(1).Delete(organization);
		await _keycloakService.Received(1).DeleteOrganizationAsync(orgId, cancellationToken);
		await _dbContext.Received(1).RemoveDashboardLayoutsForOrganizationAsync(
			OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{orgId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		_dbContext
			.IsOrganizerAsync(OrganizationId.Create(orgId).GetValueOrThrow(), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOtherMembersRemain(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value, Guid.NewGuid());
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*sole remaining member*");
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
		await _keycloakService.DidNotReceive().DeleteOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDeleteOrganization_WhenIsAdminAndNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: the acting admin is not a member of the organization at all.
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		_dbContext
			.IsOrganizerAsync(OrganizationId.Create(orgId).GetValueOrThrow(), DefaultRequestingUserId, Arg.Any<CancellationToken>())
			.Returns(false);
		SetMembers(orgId, Guid.NewGuid());
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId, IsAdmin: true);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_organizationRepo.Received(1).Delete(organization);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityHasFutureTimeSlotOrActiveEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value);
		var blockingOpportunity = CreateOpportunityWithFutureTimeSlot(OrganizationId.Create(orgId).GetValueOrThrow());
		_dbContext
			.GetBlockingOpportunitiesForOrganizationAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken)
			.Returns([blockingOpportunity]);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Titel*");
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
		await _keycloakService.DidNotReceive().DeleteOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}
}
