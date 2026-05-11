using Application.Common.Persistence;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.DeleteVolunteerOpportunity;

public class DeleteVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly DeleteVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = new("Hauptstraße", "1", "12345", "Berlin");
	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());

	public DeleteVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_sut = new DeleteVolunteerOpportunityCommandHandler(_dbContext);
	}

	private static VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist);

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		// Act
		var result = await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId), cancellationToken);

		// Assert
		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCallDelete_OnRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		// Act
		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId), cancellationToken);

		// Assert
		_opportunityRepo.Received(1).Delete(opportunity);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldNotCallDelete_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		try { await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId), cancellationToken); }
		catch (DomainException) { }

		// Assert
		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
	}
}
