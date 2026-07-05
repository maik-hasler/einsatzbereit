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
	private readonly DeleteOrganizationCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public DeleteOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_organizationRepo);
		_dbContext
			.GetBlockingOpportunitiesForOrganizationAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(new List<VolunteerOpportunity>());
		_sut = new DeleteOrganizationCommandHandler(_dbContext, _keycloakService);
	}

	private void AllowRequestingUserInOrg(Guid orgId) =>
		_keycloakService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(orgId, "Test Org")]);

	private void SetMembers(Guid orgId, params Guid[] memberIds) =>
		_keycloakService
			.GetMembersAsync(orgId, Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<KeycloakOrganizationMember>)memberIds
				.Select(id => new KeycloakOrganizationMember(id, "user", "First", "Last", "user@example.com", false))
				.ToList());

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(new OrganizationId(id), "Test Org");

	private static VolunteerOpportunity CreateOpportunityWithFutureTimeSlot(OrganizationId orgId)
	{
		var opportunity = VolunteerOpportunity.Create(
			orgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, status: OpportunityStatus.Draft);
		opportunity.AddTimeSlot(DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 5);
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldDeleteOrganizationAndCallKeycloak_WhenSoleMemberAndNoBlockingOpportunities(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		_organizationRepo.Received(1).Delete(organization);
		await _keycloakService.Received(1).DeleteOrganizationAsync(orgId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		_organizationRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns((Organization?)null);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{orgId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMember(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(organization);
		_keycloakService
			.GetUserOrganizationsAsync(DefaultRequestingUserId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(Guid.NewGuid(), "Unrelated Org")]);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>();
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOtherMembersRemain(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value, Guid.NewGuid());
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*sole remaining member*");
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
		await _keycloakService.DidNotReceive().DeleteOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityHasFutureTimeSlotOrActiveEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(new OrganizationId(orgId), cancellationToken).Returns(organization);
		AllowRequestingUserInOrg(orgId);
		SetMembers(orgId, DefaultRequestingUserId.Value);
		var blockingOpportunity = CreateOpportunityWithFutureTimeSlot(new OrganizationId(orgId));
		_dbContext
			.GetBlockingOpportunitiesForOrganizationAsync(new OrganizationId(orgId), cancellationToken)
			.Returns([blockingOpportunity]);
		var command = new DeleteOrganizationCommand(orgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Titel*");
		_organizationRepo.DidNotReceive().Delete(Arg.Any<Organization>());
		await _keycloakService.DidNotReceive().DeleteOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}
}
