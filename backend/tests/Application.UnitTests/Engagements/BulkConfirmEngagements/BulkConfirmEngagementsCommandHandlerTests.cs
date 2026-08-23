using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.BulkConfirmEngagements.v1;
using Application.Engagements.ConfirmEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.BulkConfirmEngagements;

public class BulkConfirmEngagementsCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly BulkConfirmEngagementsCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly VolunteerOpportunityId DefaultOpportunityId = VolunteerOpportunityId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public BulkConfirmEngagementsCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Engagements.Returns(_engagementRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new BulkConfirmEngagementsCommandHandler(_dbContext, _sender);
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", null, "Test", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private static Engagement CreateConfirmedEngagement(VolunteerOpportunityId? opportunityId = null)
	{
		var engagement = Engagement.CreateSlotSignUp(opportunityId ?? DefaultOpportunityId, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		return engagement;
	}

	private void SeedEngagement(Engagement engagement) =>
		_engagementRepo.FindAsync(engagement.Id, Arg.Any<CancellationToken>()).Returns(engagement);

	[Test]
	public async Task Handle_ShouldReturnSucceeded_ForEveryEngagementConfirmedByTheNestedCommand(
		CancellationToken cancellationToken)
	{
		var engagementA = CreateConfirmedEngagement();
		var engagementB = CreateConfirmedEngagement();
		SeedEngagement(engagementA);
		SeedEngagement(engagementB);
		_sender.Send(Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == engagementA.Id), Arg.Any<CancellationToken>())
			.Returns(engagementA);
		_sender.Send(Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == engagementB.Id), Arg.Any<CancellationToken>())
			.Returns(engagementB);

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[engagementA.Id, engagementB.Id],
			DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Failed.Should().BeEmpty();
		result.Succeeded.Should().HaveCount(2);
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementA.Id.Value && s.Status == "Confirmed");
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementB.Id.Value && s.Status == "Confirmed");
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutAbortingTheRestOfTheBatch_WhenANestedConfirmFails(
		CancellationToken cancellationToken)
	{
		var okEngagement = CreateConfirmedEngagement();
		var badEngagement = CreateConfirmedEngagement();
		SeedEngagement(okEngagement);
		SeedEngagement(badEngagement);
		_sender.Send(Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == okEngagement.Id), Arg.Any<CancellationToken>())
			.Returns(okEngagement);
		_sender.Send(Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == badEngagement.Id), Arg.Any<CancellationToken>())
			.Returns<Engagement>(_ => throw new ResultFailureException(Error.Conflict("Engagement.NotPending", "Only pending engagements can be confirmed.")));

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[okEngagement.Id, badEngagement.Id],
			DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Succeeded.Should().ContainSingle(s => s.EngagementId == okEngagement.Id.Value);
		result.Failed.Should().ContainSingle(f => f.EngagementId == badEngagement.Id.Value
			&& f.ErrorCode == "Engagement.NotPending"
			&& f.Message == "Only pending engagements can be confirmed.");
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutCallingTheNestedCommand_WhenEngagementDoesNotExist(
		CancellationToken cancellationToken)
	{
		var missingId = EngagementId.New();
		_engagementRepo.FindAsync(missingId, Arg.Any<CancellationToken>()).Returns((Engagement?)null);

		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [missingId], DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == missingId.Value && f.ErrorCode == "Engagement.NotFound");
		await _sender.DidNotReceive().Send(Arg.Any<ConfirmEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutCallingTheNestedCommand_WhenEngagementBelongsToADifferentOpportunity(
		CancellationToken cancellationToken)
	{
		var foreignEngagement = CreateConfirmedEngagement(VolunteerOpportunityId.New());
		SeedEngagement(foreignEngagement);

		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [foreignEngagement.Id], DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == foreignEngagement.Id.Value && f.ErrorCode == "Engagement.WrongOpportunity");
		await _sender.DidNotReceive().Send(Arg.Any<ConfirmEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSendEachDistinctEngagementIdOnlyOnce_WhenDuplicatesArePassed(
		CancellationToken cancellationToken)
	{
		var engagement = CreateConfirmedEngagement();
		SeedEngagement(engagement);
		_sender.Send(Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == engagement.Id), Arg.Any<CancellationToken>())
			.Returns(engagement);

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[engagement.Id, engagement.Id],
			DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Succeeded.Should().ContainSingle();
		await _sender.Received(1).Send(
			Arg.Is<ConfirmEngagementCommand>(c => c!.EngagementId == engagement.Id),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		_opportunityRepo.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var opportunityId = VolunteerOpportunityId.New();
		var command = new BulkConfirmEngagementsCommand(opportunityId, [EngagementId.New()], DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{opportunityId.Value}*");
		await _sender.DidNotReceive().Send(Arg.Any<ConfirmEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [EngagementId.New()], DefaultRequestingUserId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
		await _sender.DidNotReceive().Send(Arg.Any<ConfirmEngagementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPassTimezoneThrough_ToEachNestedConfirmCommand(
		CancellationToken cancellationToken)
	{
		var engagement = CreateConfirmedEngagement();
		SeedEngagement(engagement);
		_sender.Send(Arg.Any<ConfirmEngagementCommand>(), Arg.Any<CancellationToken>())
			.Returns(engagement);
		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[engagement.Id],
			DefaultRequestingUserId,
			"Europe/Berlin");

		await _sut.Handle(command, cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<ConfirmEngagementCommand>(c => c!.Timezone == "Europe/Berlin"),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyResult_WhenNoEngagementIdsProvided(
		CancellationToken cancellationToken)
	{
		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [], DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().BeEmpty();
	}
}
