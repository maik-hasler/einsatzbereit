using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.PublishVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.PublishVolunteerOpportunity;

public class PublishVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly PublishVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public PublishVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new PublishVolunteerOpportunityCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateDraftOpportunity(
		string title = "Titel",
		string description = "Beschreibung",
		ParticipationType participationType = ParticipationType.IndividualContact) =>
		VolunteerOpportunity.Create(
			DefaultOrgId, title, null, description, null, false, DefaultAddress, Occurrence.OneTime, participationType, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft,
			validUntil: participationType == ParticipationType.IndividualContact ? DateTimeOffset.UtcNow.AddDays(30) : null).Value;

	private void SetupOpportunity(Guid opportunityId, VolunteerOpportunity opportunity) =>
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<CancellationToken>())
			.Returns(opportunity);

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenOpportunityIsPublishable(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		var result = await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldSetStatusToPublished_WhenOpportunityIsPublishable(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	[Test]
	public async Task Handle_ShouldRaisePublishedDomainEvent_WhenOpportunityIsPublishable(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// CreateDraftOpportunity's non-remote address also raises a
		// VolunteerOpportunityGeocodingRequestedDomainEvent at creation time (see
		// VolunteerOpportunity.Create) - filter to the Published event specifically.
		var published = opportunity.Events.OfType<VolunteerOpportunityPublishedDomainEvent>().Should().ContainSingle().Which;
		published.OpportunityId.Should().Be(opportunity.Id);
		published.OrganizationId.Should().Be(DefaultOrgId);
	}

	[Test]
	public async Task Handle_ShouldPublishScheduledSlotsOpportunity_WhenAtLeastOneTimeSlotExists(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity(participationType: ParticipationType.ScheduledSlots);
		opportunity.AddTimeSlot(DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(7).AddHours(2), 10, DateTimeOffset.UtcNow);
		SetupOpportunity(opportunityId, opportunity);

		var result = await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		result.Should().BeTrue();
		opportunity.Status.Should().Be(OpportunityStatus.Published);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*permission*");
	}

	[Test]
	public async Task Handle_ShouldNotChangeStatus_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		SetupOpportunity(opportunityId, opportunity);

		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);

		try { await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken); }
		catch (ResultFailureException) { }

		opportunity.Status.Should().Be(OpportunityStatus.Draft);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenAlreadyPublished(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		opportunity.Publish();
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*already published*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTitleIsEmpty(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity(title: "");
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Title must not be empty*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenDescriptionIsEmpty(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity(description: "");
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Description must not be empty*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNonRemoteAndNoAddress(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, false, address: null, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Address is required*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenScheduledSlotsParticipationType_AndNoTimeSlots(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity(participationType: ParticipationType.ScheduledSlots);
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Scheduled slots opportunity must have at least one time slot*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenIndividualContactParticipationType_AndNoValidUntil(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		SetupOpportunity(opportunityId, opportunity);

		Func<Task> act = async () => await _sut.Handle(new PublishVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Individual contact opportunity must have a deadline*");
	}
}
