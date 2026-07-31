using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.UnpublishVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.UnpublishVolunteerOpportunity;

public class UnpublishVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly UnpublishVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public UnpublishVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new UnpublishVolunteerOpportunityCommandHandler(_dbContext);
	}

	private static VolunteerOpportunity CreatePublishedOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Draft,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;
		opportunity.Publish();
		return opportunity;
	}

	private void SetupOpportunity(Guid opportunityId, VolunteerOpportunity opportunity) =>
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<CancellationToken>())
			.Returns(opportunity);

	[Test]
	public async Task Handle_ShouldReturnTrue_AndSetStatusToUnpublished_WhenOpportunityIsPublished(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		var result = await _sut.Handle(new UnpublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Unpublished);
	}

	[Test]
	public async Task Handle_ShouldRaiseUnpublishedDomainEvent_WhenOpportunityIsPublished(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		await _sut.Handle(new UnpublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert - opportunity.Events also carries the Published event raised by
		// CreatePublishedOpportunity()'s own Publish() call, so assert the
		// Unpublished event was added rather than that it's the only one.
		opportunity.Events.Should().ContainSingle(e => e is VolunteerOpportunityUnpublishedDomainEvent);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityIsDraft(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;
		SetupOpportunity(opportunityId, opportunity);

		// Act
		Func<Task> act = async () => await _sut.Handle(new UnpublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new UnpublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);

		// Act
		Func<Task> act = async () => await _sut.Handle(new UnpublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}
}
