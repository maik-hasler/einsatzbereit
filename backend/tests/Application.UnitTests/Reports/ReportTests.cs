using AwesomeAssertions;
using Domain.Reports;
using Domain.Users;

namespace Application.UnitTests.Reports;

public class ReportTests
{
	[Test]
	public void Create_ShouldSucceed_WithSpamReasonAndNoDetail()
	{
		// Act
		var result = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null);

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Value.Status.Should().Be(ReportStatus.Pending);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public void Create_ShouldFail_WhenReasonIsOtherAndDetailIsMissing(string? detail)
	{
		// Act
		var result = Report.Create(ReportedContentType.Organization, Guid.NewGuid(), UserId.New(), ReportReason.Other, detail);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("A detail description is required when the reason is 'Other'.");
	}

	[Test]
	public void Create_ShouldSucceed_WhenReasonIsOtherAndDetailIsProvided()
	{
		// Act
		var result = Report.Create(ReportedContentType.Organization, Guid.NewGuid(), UserId.New(), ReportReason.Other, "Looks like a scam.");

		// Assert
		result.IsSuccess.Should().BeTrue();
		result.Value.Detail.Should().Be("Looks like a scam.");
	}

	[Test]
	public void Create_ShouldFail_WhenDetailExceedsMaxLength()
	{
		// Arrange
		var detail = new string('a', Report.MaxDetailLength + 1);

		// Act
		var result = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Fraud, detail);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be($"Detail must not exceed {Report.MaxDetailLength} characters.");
	}

	[Test]
	public void Resolve_ShouldSetStatusToResolved_WhenPending()
	{
		// Arrange
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;

		// Act
		var result = report.Resolve();

		// Assert
		result.IsSuccess.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Resolved);
	}

	[Test]
	public void Dismiss_ShouldSetStatusToDismissed_WhenPending()
	{
		// Arrange
		var report = Report.Create(ReportedContentType.Organization, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;

		// Act
		var result = report.Dismiss();

		// Assert
		result.IsSuccess.Should().BeTrue();
		report.Status.Should().Be(ReportStatus.Dismissed);
	}

	[Test]
	public void Resolve_ShouldFail_WhenAlreadyResolved()
	{
		// Arrange
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		report.Resolve();

		// Act
		var result = report.Resolve();

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Only pending reports can be resolved.");
	}

	[Test]
	public void Dismiss_ShouldFail_WhenAlreadyDismissed()
	{
		// Arrange
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		report.Dismiss();

		// Act
		var result = report.Dismiss();

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Only pending reports can be dismissed.");
	}

	[Test]
	public void Dismiss_ShouldFail_WhenAlreadyResolved()
	{
		// Arrange
		var report = Report.Create(ReportedContentType.VolunteerOpportunity, Guid.NewGuid(), UserId.New(), ReportReason.Spam, null).Value;
		report.Resolve();

		// Act
		var result = report.Dismiss();

		// Assert
		result.IsFailure.Should().BeTrue();
	}
}
