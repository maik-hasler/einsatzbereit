using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Organizations.CreateOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using NSubstitute;
using NSubstitute.ExceptionExtensions;


namespace Application.UnitTests.Organizations.CreateOrganization;

public class CreateOrganizationCommandHandlerTests
{
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<OrganizationMembership, OrganizationMembershipId> _membershipRepo =
		Substitute.For<IAggregateRepository<OrganizationMembership, OrganizationMembershipId>>();
	private readonly CreateOrganizationCommandHandler _sut;

	public CreateOrganizationCommandHandlerTests()
	{
		_dbContext.OrganizationMemberships.Returns(_membershipRepo);
		_sut = new CreateOrganizationCommandHandler(
			_keycloakService,
			_dbContext);
	}

	[Test]
	public async Task Handle_ShouldCreateOrganizationInKeycloakAndDatabase(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Sample Fire Department", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Sample Fire Department", cancellationToken)
			.Returns(keycloakId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Name.Should().Be("Sample Fire Department");
	}

	[Test]
	public async Task Handle_ShouldAddCreatorAsMemberInKeycloak(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _keycloakService.Received(1).AddMemberAsync(keycloakId, userId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldAssignOrganizerRoleToCreator(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _keycloakService.Received(1).AssignOrganizerRoleAsync(userId, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCreateOrganizerMembership_ForCreator(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _membershipRepo.Received(1).AddAsync(
			Arg.Is<OrganizationMembership>(m =>
				m!.OrganizationId == OrganizationId.Create(keycloakId).GetValueOrThrow() &&
				m.UserId == Domain.Users.UserId.Create(userId).GetValueOrThrow() &&
				m.Role == OrganizationMemberRole.Organizer),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldPersistOrganizationToRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext.Organizations.Received(1).AddAsync(
			Arg.Is<Organization>(o => o!.Name == "Test Org"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCallKeycloakOperationsInCorrectOrder(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);
		var callOrder = new List<string>();

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(_ =>
			{
				callOrder.Add("CreateOrganization");
				return keycloakId;
			});

		_keycloakService
			.When(x => x.AddMemberAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), cancellationToken))
			.Do(_ => callOrder.Add("AddMember"));

		_keycloakService
			.When(x => x.AssignOrganizerRoleAsync(Arg.Any<Guid>(), cancellationToken))
			.Do(_ => callOrder.Add("AssignRole"));

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		callOrder.Should().Equal(
			"CreateOrganization", "AddMember", "AssignRole");
	}

	[Test]
	public async Task Handle_ShouldPropagateException_WhenKeycloakCreateFails(
		CancellationToken cancellationToken)
	{
		// Arrange
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Bad Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Bad Org", cancellationToken)
			.ThrowsAsync(new HttpRequestException("Keycloak responded with 400 BadRequest"));

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<HttpRequestException>();
		await _dbContext.Organizations.DidNotReceive().AddAsync(
			Arg.Any<Organization>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPersistOptionalFields_WhenProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand(
			"Test Org",
			userId,
			"A helpful description",
			"contact@example.com",
			"+49 30 1234567",
			"https://example.com",
			new CreateAddressCommand("Main Street", "1", "12345", "Berlin"));

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Description.Should().Be("A helpful description");
		result.ContactEmail.Should().Be("contact@example.com");
		result.ContactPhone.Should().Be("+49 30 1234567");
		result.Website.Should().Be("https://example.com");
		result.Address.Should().NotBeNull();
		result.Address!.City.Should().Be("Berlin");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenWebsiteIsInvalid(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand(
			"Test Org", userId, null, null, null, "javascript:alert(1)", null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Website must be a valid http or https URL*");
	}

	[Test]
	public async Task Handle_ShouldPropagateException_WhenAddMemberFails(
		CancellationToken cancellationToken)
	{
		// Arrange
		var keycloakId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		var command = new CreateOrganizationCommand("Test Org", userId, null, null, null, null, null);

		_keycloakService
			.CreateOrganizationAsync("Test Org", cancellationToken)
			.Returns(keycloakId);

		_keycloakService
			.AddMemberAsync(keycloakId, userId, cancellationToken)
			.ThrowsAsync(new HttpRequestException("User does not exist"));

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<HttpRequestException>()
			.WithMessage("*User does not exist*");
	}
}
