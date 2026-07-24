using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.VerifyOrganization.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using NSubstitute;

namespace Application.UnitTests.Organizations.VerifyOrganization;

public class VerifyOrganizationCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Organization, OrganizationId> _organizationRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly VerifyOrganizationCommandHandler _sut;

	public VerifyOrganizationCommandHandlerTests()
	{
		_dbContext.Organizations.Returns(_organizationRepo);
		_sut = new VerifyOrganizationCommandHandler(_dbContext);
	}

	private static Organization CreateOrganization(Guid id) =>
		Organization.Create(OrganizationId.Create(id).GetValueOrThrow(), "Test Org").Value;

	[Test]
	public async Task Handle_ShouldVerifyOrganization_WhenIsVerifiedIsTrue(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		var command = new VerifyOrganizationCommand(orgId, true);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		organization.IsVerified.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldRevokeVerification_WhenIsVerifiedIsFalse(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		organization.Verify();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		var command = new VerifyOrganizationCommand(orgId, false);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		organization.IsVerified.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);
		var command = new VerifyOrganizationCommand(orgId, true);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{orgId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenAlreadyVerifiedAndVerifyingAgain(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		organization.Verify();
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		var command = new VerifyOrganizationCommand(orgId, true);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*already verified*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNotVerifiedAndRevokingAgain(
		CancellationToken cancellationToken)
	{
		// Arrange
		var orgId = Guid.NewGuid();
		var organization = CreateOrganization(orgId);
		_organizationRepo.FindAsync(OrganizationId.Create(orgId).GetValueOrThrow(), cancellationToken).Returns(organization);
		var command = new VerifyOrganizationCommand(orgId, false);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*not verified*");
	}
}
