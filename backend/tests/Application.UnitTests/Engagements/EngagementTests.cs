using AwesomeAssertions;
using Domain.Engagements;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.Engagements;

public class EngagementTests
{
	private static VolunteerOpportunityId AnyOpportunityId() =>
		VolunteerOpportunityId.New();

	private static Domain.Users.UserId AnyUserId() =>
		Domain.Users.UserId.New();

	private static TimeSlotId AnyTimeSlotId() =>
		TimeSlotId.New();

	[Test]
	public void CreateSlotSignUp_ShouldCreateEngagement_WithPendingStatus()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void CreateSlotSignUp_ShouldSetTimeSlotId()
	{
		var timeSlotId = AnyTimeSlotId();

		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), timeSlotId);

		engagement.TimeSlotId.Should().Be(timeSlotId);
	}

	[Test]
	public void CreateSlotSignUp_ShouldNotSetMessage()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Message.Should().BeNull();
	}

	[Test]
	public void CreateIndividualContact_ShouldCreateEngagement_WithPendingStatus()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich möchte gerne helfen.");

		result.Value.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void CreateIndividualContact_ShouldSetMessage()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Ich bin verfügbar.");

		result.Value.Message.Should().Be("Ich bin verfügbar.");
	}

	[Test]
	public void CreateIndividualContact_ShouldNotSetTimeSlotId()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Nachricht");

		result.Value.TimeSlotId.Should().BeNull();
	}

	[Test]
	public void CreateIndividualContact_ShouldFail_WhenMessageIsEmpty()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "");

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*message is required*");
	}

	[Test]
	public void CreateIndividualContact_ShouldFail_WhenMessageIsWhitespace()
	{
		var result = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "   ");

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	public void Confirm_ShouldSetStatus_ToConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Confirm();

		engagement.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public void Confirm_ShouldFail_WhenAlreadyConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldFail_WhenCancelled()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
	}

	[Test]
	public void Confirm_ShouldFail_WhenWithdrawn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only pending*");
	}

	[Test]
	public void Cancel_ShouldSetStatus_ToCancelled_WhenPending()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Cancel();

		engagement.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public void Cancel_ShouldSetStatus_ToCancelled_WhenConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.Cancel();

		engagement.Status.Should().Be(EngagementStatus.Cancelled);
	}

	[Test]
	public void Cancel_ShouldFail_WhenAlreadyCancelled()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Cancel_ShouldFail_WhenWithdrawn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldSetStatus_ToWithdrawn_WhenPending()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Withdraw();

		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public void Withdraw_ShouldSetStatus_ToWithdrawn_WhenConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.Withdraw();

		engagement.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public void Withdraw_ShouldFail_WhenAlreadyWithdrawn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldFail_WhenCancelled()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already terminated*");
	}

	[Test]
	public void Withdraw_ShouldFail_WhenCheckedIn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*checked-in*");
	}

	[Test]
	public void Reactivate_ShouldSetStatus_ToPending()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.Status.Should().Be(EngagementStatus.Pending);
	}

	[Test]
	public void Reactivate_ShouldNotChangeCreatedOn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();
		var originalCreatedOn = engagement.CreatedOn;

		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.CreatedOn.Should().Be(originalCreatedOn);
	}

	[Test]
	public void Reactivate_ShouldFail_WhenPending()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		var result = engagement.Reactivate(AnyTimeSlotId(), message: null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*withdrawn or cancelled*");
	}

	[Test]
	public void Reactivate_ShouldIncrementReactivationCount()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();

		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.ReactivationCount.Should().Be(1);
	}

	[Test]
	public void Reactivate_ShouldFail_WhenReactivationLimitReached()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		for (var i = 0; i < 5; i++)
		{
			engagement.Withdraw();
			engagement.Reactivate(AnyTimeSlotId(), message: null).IsSuccess.Should().BeTrue();
		}

		engagement.Withdraw();
		var result = engagement.Reactivate(AnyTimeSlotId(), message: null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*too many times*");
	}

	[Test]
	public void Reactivate_ShouldNotIncrementReactivationCount_WhenReactivationLimitReached()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		for (var i = 0; i < 5; i++)
		{
			engagement.Withdraw();
			engagement.Reactivate(AnyTimeSlotId(), message: null);
		}

		engagement.Withdraw();
		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.ReactivationCount.Should().Be(5);
	}

	[Test]
	public void Reactivate_ShouldNotIncrementReactivationCount_WhenPreviouslyCancelledByOrganizer()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();

		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.ReactivationCount.Should().Be(0);
	}

	[Test]
	public void Reactivate_ShouldNotHitReactivationLimit_WhenOrganizerRepeatedlyCancels()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		for (var i = 0; i < 10; i++)
		{
			engagement.Cancel();
			engagement.Reactivate(AnyTimeSlotId(), message: null).IsSuccess.Should().BeTrue();
		}

		engagement.ReactivationCount.Should().Be(0);
	}

	[Test]
	public void Reactivate_ShouldStillIncrementReactivationCount_WhenVolunteerWithdrawsAfterAnOrganizerCancellation()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Cancel();
		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.Withdraw();
		engagement.Reactivate(AnyTimeSlotId(), message: null);

		engagement.ReactivationCount.Should().Be(1);
	}

	[Test]
	public void CheckIn_ShouldSetIsCheckedIn_WhenConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.CheckIn(DateTimeOffset.UtcNow);

		engagement.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public void CheckIn_ShouldFail_WhenNotConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		var result = engagement.CheckIn(DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only confirmed*");
	}

	[Test]
	public void CheckIn_ShouldFail_WhenAlreadyCheckedIn()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);

		var result = engagement.CheckIn(DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already checked in*");
	}

	private static Engagement CreateConfirmedSlotEngagement(DateTimeOffset start, DateTimeOffset end)
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId(), start, end);
		engagement.Confirm();
		return engagement;
	}

	[Test]
	public void CheckIn_ShouldFail_WhenBeforeTheCheckInWindowOpens()
	{
		var start = DateTimeOffset.UtcNow.AddHours(3);
		var engagement = CreateConfirmedSlotEngagement(start, start.AddHours(2));

		var result = engagement.CheckIn(start - TimeSlot.CheckInWindowBefore - TimeSpan.FromMinutes(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*only possible shortly before*");
	}

	[Test]
	public void CheckIn_ShouldSucceed_AtTheCheckInWindowOpenBoundary()
	{
		var start = DateTimeOffset.UtcNow.AddHours(3);
		var engagement = CreateConfirmedSlotEngagement(start, start.AddHours(2));

		var result = engagement.CheckIn(start - TimeSlot.CheckInWindowBefore);

		result.IsFailure.Should().BeFalse();
	}

	[Test]
	public void CheckIn_ShouldSucceed_AtTheCheckInWindowCloseBoundary()
	{
		var start = DateTimeOffset.UtcNow.AddHours(-3);
		var end = start.AddHours(2);
		var engagement = CreateConfirmedSlotEngagement(start, end);

		var result = engagement.CheckIn(end + TimeSlot.CheckInWindowAfter);

		result.IsFailure.Should().BeFalse();
	}

	[Test]
	public void CheckIn_ShouldFail_AfterTheCheckInWindowCloses()
	{
		var start = DateTimeOffset.UtcNow.AddHours(-3);
		var end = start.AddHours(2);
		var engagement = CreateConfirmedSlotEngagement(start, end);

		var result = engagement.CheckIn(end + TimeSlot.CheckInWindowAfter + TimeSpan.FromMinutes(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*only possible shortly before*");
	}

	[Test]
	public void CheckIn_ShouldFail_ForAFutureOccurrenceMonthsAhead()
	{
		var start = DateTimeOffset.UtcNow.AddMonths(3);
		var engagement = CreateConfirmedSlotEngagement(start, start.AddHours(2));

		var result = engagement.CheckIn(DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*only possible shortly before*");
	}

	[Test]
	public void CheckIn_ShouldSucceed_ForIndividualContactEngagement_RegardlessOfNow()
	{
		var engagement = Engagement.CreateIndividualContact(AnyOpportunityId(), AnyUserId(), "Nachricht").Value;
		engagement.Confirm();

		var result = engagement.CheckIn(DateTimeOffset.UtcNow.AddYears(1));

		result.IsFailure.Should().BeFalse();
	}

	[Test]
	public void Anonymize_ShouldSetVolunteerIdToNull()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		engagement.Anonymize();

		engagement.IsAnonymized.Should().BeTrue();
	}

	[Test]
	public void Anonymize_ShouldClearMessageAndFeedback()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);
		engagement.SubmitFeedback(5, "Great!", DateTimeOffset.UtcNow);

		engagement.Anonymize();

		engagement.Message.Should().BeNull();
		engagement.FeedbackComment.Should().BeNull();
		engagement.FeedbackRating.Should().BeNull();
		engagement.FeedbackSubmittedAt.Should().BeNull();
	}

	[Test]
	public void Confirm_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Anonymize();

		var result = engagement.Confirm();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void Cancel_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Anonymize();

		var result = engagement.Cancel();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void Withdraw_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Anonymize();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void Reactivate_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Withdraw();
		engagement.Anonymize();

		var result = engagement.Reactivate(AnyTimeSlotId(), message: null);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void CheckIn_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.Anonymize();

		var result = engagement.CheckIn(DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void UndoCheckIn_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);
		engagement.Anonymize();

		var result = engagement.UndoCheckIn();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void SubmitFeedback_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);
		engagement.Anonymize();

		var result = engagement.SubmitFeedback(5, "Great shift", DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	private static Engagement CreateCheckedInEngagementWithFeedback(int rating, string? comment, DateTimeOffset submittedAt)
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);
		engagement.SubmitFeedback(rating, comment, submittedAt);
		return engagement;
	}

	[Test]
	public void UpdateFeedback_ShouldUpdateRatingAndComment_WhenWithinEditWindow()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.UpdateFeedback(5, "Actually, great!", submittedAt.AddHours(1));

		result.IsFailure.Should().BeFalse();
		engagement.FeedbackRating.Should().Be(5);
		engagement.FeedbackComment.Should().Be("Actually, great!");
	}

	[Test]
	public void UpdateFeedback_ShouldNotChangeFeedbackSubmittedAt()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		engagement.UpdateFeedback(5, "Actually, great!", submittedAt.AddHours(1));

		engagement.FeedbackSubmittedAt.Should().Be(submittedAt);
	}

	[Test]
	public void UpdateFeedback_ShouldFail_WhenFeedbackNotYetSubmitted()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);

		var result = engagement.UpdateFeedback(5, "Great!", DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*not been submitted*");
	}

	[Test]
	public void UpdateFeedback_ShouldSucceed_AtExactEditWindowBoundary()
	{
		var submittedAt = DateTimeOffset.UtcNow;
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.UpdateFeedback(5, "Better!", submittedAt.AddDays(Engagement.FeedbackEditWindowDays));

		result.IsFailure.Should().BeFalse();
	}

	[Test]
	public void UpdateFeedback_ShouldFail_WhenEditWindowExpired()
	{
		var submittedAt = DateTimeOffset.UtcNow;
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.UpdateFeedback(
			5,
			"Better!",
			submittedAt.AddDays(Engagement.FeedbackEditWindowDays).AddSeconds(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*no longer be edited*");
	}

	[Test]
	[Arguments(0)]
	[Arguments(-1)]
	[Arguments(6)]
	public void UpdateFeedback_ShouldFail_WhenRatingIsOutOfRange(int invalidRating)
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.UpdateFeedback(invalidRating, null, submittedAt.AddHours(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Rating must be between 1 and 5*");
	}

	[Test]
	public void UpdateFeedback_ShouldFail_WhenCommentExceedsMaxLength()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);
		var tooLongComment = new string('a', 501);

		var result = engagement.UpdateFeedback(4, tooLongComment, submittedAt.AddHours(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*must not exceed 500 characters*");
	}

	[Test]
	public void UpdateFeedback_ShouldFail_WhenAnonymized()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);
		engagement.Anonymize();

		var result = engagement.UpdateFeedback(5, "Great!", submittedAt.AddHours(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void DeleteFeedback_ShouldClearFeedbackFields_WhenWithinEditWindow()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.DeleteFeedback(submittedAt.AddHours(1));

		result.IsFailure.Should().BeFalse();
		engagement.FeedbackRating.Should().BeNull();
		engagement.FeedbackComment.Should().BeNull();
		engagement.FeedbackSubmittedAt.Should().BeNull();
	}

	[Test]
	public void DeleteFeedback_ShouldAllowResubmission_Afterward()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);
		engagement.DeleteFeedback(submittedAt.AddHours(1));

		var result = engagement.SubmitFeedback(4, "Second try", submittedAt.AddHours(2));

		result.IsFailure.Should().BeFalse();
		engagement.FeedbackRating.Should().Be(4);
	}

	[Test]
	public void DeleteFeedback_ShouldFail_WhenFeedbackNotYetSubmitted()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn(DateTimeOffset.UtcNow);

		var result = engagement.DeleteFeedback(DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*not been submitted*");
	}

	[Test]
	public void DeleteFeedback_ShouldFail_WhenEditWindowExpired()
	{
		var submittedAt = DateTimeOffset.UtcNow;
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);

		var result = engagement.DeleteFeedback(
			submittedAt.AddDays(Engagement.FeedbackEditWindowDays).AddSeconds(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*no longer be edited*");
	}

	[Test]
	public void DeleteFeedback_ShouldFail_WhenAnonymized()
	{
		var submittedAt = DateTimeOffset.UtcNow.AddDays(-1);
		var engagement = CreateCheckedInEngagementWithFeedback(3, "Okay", submittedAt);
		engagement.Anonymize();

		var result = engagement.DeleteFeedback(submittedAt.AddHours(1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}
}
