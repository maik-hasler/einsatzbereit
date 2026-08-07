using AwesomeAssertions;
using Domain.Primitives;
using Domain.Users;

namespace Application.UnitTests.Users;

public class UserTests
{
	[Test]
	public void Create_ShouldDefaultToSubscribedToEveryNotificationType()
	{
		// Act
		var user = User.Create(UserId.New());

		// Assert
		user.NotifyOnNewSignUp.Should().BeTrue();
		user.NotifyOnWithdrawal.Should().BeTrue();
		user.NotifyOnEngagementConfirmed.Should().BeTrue();
		user.NotifyOnEngagementCancelled.Should().BeTrue();
		user.NotifyOnEngagementReminder.Should().BeTrue();
		foreach (var type in Enum.GetValues<EmailNotificationType>())
			user.IsSubscribedTo(type).Should().BeTrue();
	}

	[Test]
	public void Create_ShouldAssignAnUnsubscribeToken()
	{
		// Act
		var user = User.Create(UserId.New());

		// Assert
		user.UnsubscribeToken.Should().NotBe(Guid.Empty);
	}

	[Test]
	public void Create_ShouldAssignADifferentUnsubscribeToken_ToEachUser()
	{
		// Act
		var first = User.Create(UserId.New());
		var second = User.Create(UserId.New());

		// Assert
		first.UnsubscribeToken.Should().NotBe(second.UnsubscribeToken);
	}

	[Test]
	public void UpdateNotificationPreferences_ShouldOverwriteAllFiveFlags()
	{
		// Arrange
		var user = User.Create(UserId.New());

		// Act
		user.UpdateNotificationPreferences(
			notifyOnNewSignUp: false,
			notifyOnWithdrawal: false,
			notifyOnEngagementConfirmed: false,
			notifyOnEngagementCancelled: false,
			notifyOnEngagementReminder: false);

		// Assert
		user.NotifyOnNewSignUp.Should().BeFalse();
		user.NotifyOnWithdrawal.Should().BeFalse();
		user.NotifyOnEngagementConfirmed.Should().BeFalse();
		user.NotifyOnEngagementCancelled.Should().BeFalse();
		user.NotifyOnEngagementReminder.Should().BeFalse();
	}

	[Test]
	public void Unsubscribe_ShouldFailWithForbidden_WhenTokenDoesNotMatch()
	{
		// Arrange
		var user = User.Create(UserId.New());

		// Act
		var result = user.Unsubscribe(EmailNotificationType.NewSignUp, Guid.NewGuid());

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Type.Should().Be(ErrorType.Forbidden);
		user.NotifyOnNewSignUp.Should().BeTrue();
	}

	[Test]
	public void Unsubscribe_ShouldDisableOnlyTheRequestedType_WhenTokenMatches()
	{
		// Arrange
		var user = User.Create(UserId.New());

		// Act
		var result = user.Unsubscribe(EmailNotificationType.EngagementReminder, user.UnsubscribeToken);

		// Assert
		result.IsSuccess.Should().BeTrue();
		user.NotifyOnEngagementReminder.Should().BeFalse();
		user.NotifyOnNewSignUp.Should().BeTrue();
		user.NotifyOnWithdrawal.Should().BeTrue();
		user.NotifyOnEngagementConfirmed.Should().BeTrue();
		user.NotifyOnEngagementCancelled.Should().BeTrue();
	}

	[Test]
	public void Unsubscribe_ShouldBeIdempotent_WhenCalledTwiceForTheSameType()
	{
		// Arrange
		var user = User.Create(UserId.New());
		user.Unsubscribe(EmailNotificationType.Withdrawal, user.UnsubscribeToken);

		// Act
		var result = user.Unsubscribe(EmailNotificationType.Withdrawal, user.UnsubscribeToken);

		// Assert
		result.IsSuccess.Should().BeTrue();
		user.NotifyOnWithdrawal.Should().BeFalse();
	}

	[Test]
	public void Unsubscribe_ShouldMakeIsSubscribedToFalse_ForEveryKnownEmailNotificationType()
	{
		// Data-driven regression guard for issue #1725: Unsubscribe's switch used
		// to have no default arm, so a future EmailNotificationType member would
		// fall through returning Result.Success() while leaving IsSubscribedTo
		// true - a silently-failed opt-out. Iterating Enum.GetValues() means this
		// test starts failing the moment a new member is added without also
		// wiring up Unsubscribe for it, rather than only when someone remembers
		// to hand-write a case for it.
		foreach (var type in Enum.GetValues<EmailNotificationType>())
		{
			var user = User.Create(UserId.New());

			var result = user.Unsubscribe(type, user.UnsubscribeToken);

			result.IsSuccess.Should().BeTrue($"Unsubscribe should succeed for {type}");
			user.IsSubscribedTo(type).Should().BeFalse($"IsSubscribedTo({type}) should be false after unsubscribing");
		}
	}
}
