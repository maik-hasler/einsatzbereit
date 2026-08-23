using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.DeleteFeedback.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.DeleteFeedback;

public class DeleteFeedbackCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly DeleteFeedbackCommandHandler _sut;

	public DeleteFeedbackCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_sut = new DeleteFeedbackCommandHandler(_dbContext);
	}

	private static (Engagement engagement, UserId volunteerId) CreateEngagementWithFeedback(
		DateTimeOffset? submittedAt = null)
	{
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		engagement.SubmitFeedback(3, "Okay", submittedAt ?? DateTimeOffset.UtcNow.AddDays(-1));
		return (engagement, volunteerId);
	}

	[Test]
	public async Task Handle_ShouldDeleteFeedback_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		var (engagement, volunteerId) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, volunteerId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().BeTrue();
		engagement.FeedbackRating.Should().BeNull();
		engagement.FeedbackComment.Should().BeNull();
		engagement.FeedbackSubmittedAt.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new DeleteFeedbackCommand(engagementId, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*"))
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCallerIsNotTheEngagementOwner(
		CancellationToken cancellationToken)
	{
		var (engagement, _) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*You can only delete feedback for your own engagements*"))
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}

	[Test]
	public async Task Handle_ShouldNotMutateEngagement_WhenCallerIsNotTheEngagementOwner(
		CancellationToken cancellationToken)
	{
		var (engagement, _) = CreateEngagementWithFeedback();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);
		await act.Should().ThrowAsync<ResultFailureException>();

		// Assert: the ownership guard fires before the domain method runs.
		engagement.FeedbackRating.Should().Be(3);
		engagement.FeedbackSubmittedAt.Should().NotBeNull();
	}

	[Test]
	public async Task Handle_ShouldThrowConflict_WhenEngagementIsAnonymized(
		CancellationToken cancellationToken)
	{
		var (engagement, _) = CreateEngagementWithFeedback();
		engagement.Anonymize();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, UserId.New());

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenFeedbackNotYetSubmitted(
		CancellationToken cancellationToken)
	{
		// Arrange: checked in, but never submitted feedback.
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		engagement.Confirm();
		engagement.CheckIn();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, volunteerId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*not been submitted*"))
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEditWindowExpired(
		CancellationToken cancellationToken)
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-(Engagement.FeedbackEditWindowDays + 1));
		var (engagement, volunteerId) = CreateEngagementWithFeedback(submittedAt);
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new DeleteFeedbackCommand(engagementId, volunteerId);

		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*no longer be edited*"))
			.Which.Error.Type.Should().Be(ErrorType.Conflict);
	}
}
