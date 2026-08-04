using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.BulkCancelEngagements.v1;
using Application.Engagements.CancelEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.BulkCancelEngagements;

public class BulkCancelEngagementsCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly BulkCancelEngagementsCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly VolunteerOpportunityId DefaultOpportunityId = VolunteerOpportunityId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public BulkCancelEngagementsCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Engagements.Returns(_engagementRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new BulkCancelEngagementsCommandHandler(_dbContext, _sender);
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private static Engagement CreatePendingEngagement(VolunteerOpportunityId? opportunityId = null) =>
		Engagement.CreateSlotSignUp(opportunityId ?? DefaultOpportunityId, UserId.New(), TimeSlotId.New());

	private static Engagement CreateCancelledEngagement(string? reason, VolunteerOpportunityId? opportunityId = null)
	{
		var engagement = CreatePendingEngagement(opportunityId);
		engagement.Cancel(reason);
		return engagement;
	}

	private void SeedEngagement(Engagement engagement) =>
		_engagementRepo.FindAsync(engagement.Id, Arg.Any<CancellationToken>()).Returns(engagement);

	[Test]
	public async Task Handle_ShouldReturnSucceeded_ForEveryEngagementCancelledByTheNestedCommand(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementA = CreateCancelledEngagement("No longer needed.");
		var engagementB = CreateCancelledEngagement("No longer needed.");
		SeedEngagement(engagementA);
		SeedEngagement(engagementB);
		_sender.Send(Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == engagementA.Id), Arg.Any<CancellationToken>())
			.Returns(engagementA);
		_sender.Send(Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == engagementB.Id), Arg.Any<CancellationToken>())
			.Returns(engagementB);

		var command = new BulkCancelEngagementsCommand(
			DefaultOpportunityId,
			[engagementA.Id, engagementB.Id],
			DefaultRequestingUserId,
			"No longer needed.");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Failed.Should().BeEmpty();
		result.Succeeded.Should().HaveCount(2);
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementA.Id.Value
			&& s.Status == "Cancelled"
			&& s.CancellationReason == "No longer needed.");
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementB.Id.Value
			&& s.Status == "Cancelled"
			&& s.CancellationReason == "No longer needed.");
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutAbortingTheRestOfTheBatch_WhenANestedCancelFails(
		CancellationToken cancellationToken)
	{
		// Arrange
		var okEngagement = CreateCancelledEngagement(null);
		var alreadyTerminatedEngagement = CreateCancelledEngagement(null);
		SeedEngagement(okEngagement);
		SeedEngagement(alreadyTerminatedEngagement);
		_sender.Send(Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == okEngagement.Id), Arg.Any<CancellationToken>())
			.Returns(okEngagement);
		_sender.Send(Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == alreadyTerminatedEngagement.Id), Arg.Any<CancellationToken>())
			.Returns<Engagement>(_ => throw new ResultFailureException(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated.")));

		var command = new BulkCancelEngagementsCommand(
			DefaultOpportunityId,
			[okEngagement.Id, alreadyTerminatedEngagement.Id],
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().ContainSingle(s => s.EngagementId == okEngagement.Id.Value);
		result.Failed.Should().ContainSingle(f => f.EngagementId == alreadyTerminatedEngagement.Id.Value
			&& f.ErrorCode == "Engagement.AlreadyTerminated"
			&& f.Message == "Engagement is already terminated.");
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutCallingTheNestedCommand_WhenEngagementDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var missingId = EngagementId.New();
		_engagementRepo.FindAsync(missingId, Arg.Any<CancellationToken>()).Returns((Engagement?)null);

		var command = new BulkCancelEngagementsCommand(DefaultOpportunityId, [missingId], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == missingId.Value && f.ErrorCode == "Engagement.NotFound");
		await _sender.DidNotReceive().Send(Arg.Any<CancelEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutCallingTheNestedCommand_WhenEngagementBelongsToADifferentOpportunity(
		CancellationToken cancellationToken)
	{
		// Arrange
		var foreignEngagement = CreateCancelledEngagement(null, VolunteerOpportunityId.New());
		SeedEngagement(foreignEngagement);

		var command = new BulkCancelEngagementsCommand(DefaultOpportunityId, [foreignEngagement.Id], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == foreignEngagement.Id.Value && f.ErrorCode == "Engagement.WrongOpportunity");
		await _sender.DidNotReceive().Send(Arg.Any<CancelEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSendEachDistinctEngagementIdOnlyOnce_WhenDuplicatesArePassed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = CreateCancelledEngagement(null);
		SeedEngagement(engagement);
		_sender.Send(Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == engagement.Id), Arg.Any<CancellationToken>())
			.Returns(engagement);

		var command = new BulkCancelEngagementsCommand(
			DefaultOpportunityId,
			[engagement.Id, engagement.Id],
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().ContainSingle();
		await _sender.Received(1).Send(
			Arg.Is<CancelEngagementCommand>(c => c!.EngagementId == engagement.Id),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var opportunityId = VolunteerOpportunityId.New();
		var command = new BulkCancelEngagementsCommand(opportunityId, [EngagementId.New()], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{opportunityId.Value}*");
		await _sender.DidNotReceive().Send(Arg.Any<CancelEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		_dbContext.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new BulkCancelEngagementsCommand(DefaultOpportunityId, [EngagementId.New()], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
		await _sender.DidNotReceive().Send(Arg.Any<CancelEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPassTheSharedReasonThrough_ToEachNestedCancelCommand(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = CreateCancelledEngagement("Shift was cancelled.");
		SeedEngagement(engagement);
		_sender.Send(Arg.Any<CancelEngagementCommand>(), Arg.Any<CancellationToken>())
			.Returns(engagement);
		var command = new BulkCancelEngagementsCommand(
			DefaultOpportunityId,
			[engagement.Id],
			DefaultRequestingUserId,
			"Shift was cancelled.");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _sender.Received(1).Send(
			Arg.Is<CancelEngagementCommand>(c => c!.Reason == "Shift was cancelled."),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyResult_WhenNoEngagementIdsProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new BulkCancelEngagementsCommand(DefaultOpportunityId, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().BeEmpty();
	}
}
