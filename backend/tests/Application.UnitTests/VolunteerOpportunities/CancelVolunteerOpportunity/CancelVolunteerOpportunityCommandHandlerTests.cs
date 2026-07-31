using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CancelVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CancelVolunteerOpportunity;

public class CancelVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CancelVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public CancelVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CancelVolunteerOpportunityCommandHandler(_dbContext);
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
	public async Task Handle_ShouldReturnTrue_AndSetStatusToCancelled_WithReason(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		var result = await _sut.Handle(
			new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId, "Funding fell through"), cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
		opportunity.CancellationReason.Should().Be("Funding fell through");
	}

	[Test]
	public async Task Handle_ShouldSetStatusToCancelled_WithoutReason(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
		opportunity.CancellationReason.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldRaiseCancelledDomainEvent_WithReason(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId, "Venue cancelled"), cancellationToken);

		// Assert - opportunity.Events also carries the Published event raised by
		// CreatePublishedOpportunity()'s own Publish() call, so assert the
		// Cancelled event was added rather than that it's the only one.
		var cancelled = opportunity.Events.OfType<VolunteerOpportunityCancelledDomainEvent>().Should().ContainSingle().Which;
		cancelled.Reason.Should().Be("Venue cancelled");
	}

	[Test]
	public async Task Handle_ShouldAllowCancel_WhenOpportunityIsUnpublished(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		opportunity.Unpublish();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		var result = await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Cancelled);
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
		Func<Task> act = async () => await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
		opportunity.Status.Should().Be(OpportunityStatus.Draft);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenAlreadyCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedOpportunity();
		opportunity.Cancel();
		SetupOpportunity(opportunityId, opportunity);

		// Act
		Func<Task> act = async () => await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>();
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
		Func<Task> act = async () => await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

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
		Func<Task> act = async () => await _sut.Handle(new CancelVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}
}
