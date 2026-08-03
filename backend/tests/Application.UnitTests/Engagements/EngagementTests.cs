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

	// --- CreateSlotSignUp ---

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

	// --- CreateIndividualContact ---

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

	// --- Confirm ---

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

	// --- Cancel ---

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

	// --- Withdraw ---

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
		engagement.CheckIn();

		var result = engagement.Withdraw();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*checked-in*");
	}

	// --- Reactivate ---

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

	// --- Reactivate: reactivation cap (#1174) ---
	//
	// Engagement.Reactivate lets a withdrawn/cancelled row be reused instead of
	// inserting a new one, which is what lets a volunteer loop create/withdraw
	// against the same opportunity - and every cycle mails the volunteer plus
	// every organizer of the org. This caps how many times any single
	// engagement can be recycled.

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

	// --- CheckIn ---

	[Test]
	public void CheckIn_ShouldSetIsCheckedIn_WhenConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();

		engagement.CheckIn();

		engagement.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public void CheckIn_ShouldFail_WhenNotConfirmed()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());

		var result = engagement.CheckIn();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*Only confirmed*");
	}

	[Test]
	public void CheckIn_ShouldFail_WhenAlreadyCheckedIn()
	{
		// Issue #1162: a repeated CheckIn() call used to re-raise EngagementCheckedInDomainEvent
		// every time, corrupting the audit trail and any future once-only consumer.
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn();

		var result = engagement.CheckIn();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*already checked in*");
	}

	// --- Anonymize ---

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
		engagement.CheckIn();
		engagement.SubmitFeedback(5, "Great!", DateTimeOffset.UtcNow);

		engagement.Anonymize();

		engagement.Message.Should().BeNull();
		engagement.FeedbackComment.Should().BeNull();
		engagement.FeedbackRating.Should().BeNull();
		engagement.FeedbackSubmittedAt.Should().BeNull();
	}

	// --- Anonymized guard (#1140) ---
	// DeleteMyAccountCommandHandler anonymizes an engagement (VolunteerId = null) when its
	// volunteer deletes their account. Every subsequent state transition must refuse to run
	// rather than dereference the now-null VolunteerId.

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

		var result = engagement.CheckIn();

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}

	[Test]
	public void UndoCheckIn_ShouldFail_WhenAnonymized()
	{
		var engagement = Engagement.CreateSlotSignUp(AnyOpportunityId(), AnyUserId(), AnyTimeSlotId());
		engagement.Confirm();
		engagement.CheckIn();
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
		engagement.CheckIn();
		engagement.Anonymize();

		var result = engagement.SubmitFeedback(5, "Great shift", DateTimeOffset.UtcNow);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Match("*deleted their account*");
	}
}
