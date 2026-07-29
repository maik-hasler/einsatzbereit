using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;

namespace Application.UnitTests.Organizations;

public class OrganizationTests
{
	[Test]
	public void Update_ShouldSetAllFields()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Original").Value;
		var address = Address.Create("Sample Street", "1", "12345", "Berlin").Value;

		// Act
		org.Rename("Updated");
		org.ChangeDescription("Description");
		org.ChangeContactInfo("mail@test.de", "+49 30 123", "https://test.de");
		org.Relocate(address);

		// Assert
		org.Name.Should().Be("Updated");
		org.Description.Should().Be("Description");
		org.ContactEmail.Should().Be("mail@test.de");
		org.ContactPhone.Should().Be("+49 30 123");
		org.Website.Should().Be("https://test.de");
		org.Address.Should().Be(address);
	}

	[Test]
	public void Relocate_ShouldClearAddress_WhenNullPassed()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.Relocate(Address.Create("St", "1", "12345", "City").Value);

		// Act
		org.Relocate(null);

		// Assert
		org.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Rename_ShouldFail_WhenNameIsEmpty(string? name)
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		// Act
		var result = org.Rename(name!);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Name must not be empty.");
	}

	[Test]
	public void Create_ShouldFail_WhenNameIsEmpty()
	{
		var result = Organization.Create(OrganizationId.New(), "");

		result.IsFailure.Should().BeTrue();
	}

	[Test]
	public void Create_ShouldRaiseCreatedDomainEvent()
	{
		// Arrange
		var id = OrganizationId.New();

		// Act
		var org = Organization.Create(id, "Org").Value;

		// Assert
		var domainEvent = org.Events.Should().ContainSingle().Which;
		domainEvent.Should().BeOfType<OrganizationCreatedDomainEvent>();
		((OrganizationCreatedDomainEvent)domainEvent).OrganizationId.Should().Be(id);
	}

	[Test]
	public void MarkDeleted_ShouldSetIsDeletedTrue()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		var deletedOn = DateTimeOffset.UtcNow;

		// Act
		var result = org.MarkDeleted(deletedOn);

		// Assert
		result.IsSuccess.Should().BeTrue();
		org.IsDeleted.Should().BeTrue();
		org.DeletedOn.Should().Be(deletedOn);
	}

	[Test]
	public void MarkDeleted_ShouldRaiseDeletedDomainEvent()
	{
		// Arrange
		var id = OrganizationId.New();
		var org = Organization.Create(id, "Org").Value;

		// Act
		org.MarkDeleted(DateTimeOffset.UtcNow);

		// Assert
		var domainEvent = org.Events.Should().ContainSingle(e => e is OrganizationDeletedDomainEvent).Which;
		((OrganizationDeletedDomainEvent)domainEvent).OrganizationId.Should().Be(id);
	}

	[Test]
	public void MarkDeleted_ShouldFail_WhenAlreadyDeleted()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.MarkDeleted(DateTimeOffset.UtcNow);

		// Act
		var result = org.MarkDeleted(DateTimeOffset.UtcNow);

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Organization is already shadow-deleted.");
	}

	[Test]
	public void Restore_ShouldClearDeletedState()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.MarkDeleted(DateTimeOffset.UtcNow);

		// Act
		var result = org.Restore();

		// Assert
		result.IsSuccess.Should().BeTrue();
		org.IsDeleted.Should().BeFalse();
		org.DeletedOn.Should().BeNull();
	}

	[Test]
	public void Restore_ShouldRaiseRestoredDomainEvent()
	{
		// Arrange
		var id = OrganizationId.New();
		var org = Organization.Create(id, "Org").Value;
		org.MarkDeleted(DateTimeOffset.UtcNow);

		// Act
		org.Restore();

		// Assert
		var domainEvent = org.Events.Should().ContainSingle(e => e is OrganizationRestoredDomainEvent).Which;
		((OrganizationRestoredDomainEvent)domainEvent).OrganizationId.Should().Be(id);
	}

	[Test]
	public void Restore_ShouldFail_WhenNotDeleted()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		// Act
		var result = org.Restore();

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Organization is not shadow-deleted.");
	}

	[Test]
	public void Verify_ShouldSetIsVerifiedTrue()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		// Act
		var result = org.Verify();

		// Assert
		result.IsSuccess.Should().BeTrue();
		org.IsVerified.Should().BeTrue();
	}

	[Test]
	public void Verify_ShouldFail_WhenAlreadyVerified()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.Verify();

		// Act
		var result = org.Verify();

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Organization is already verified.");
		org.IsVerified.Should().BeTrue();
	}

	[Test]
	public void RevokeVerification_ShouldSetIsVerifiedFalse()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.Verify();

		// Act
		var result = org.RevokeVerification();

		// Assert
		result.IsSuccess.Should().BeTrue();
		org.IsVerified.Should().BeFalse();
	}

	[Test]
	public void RevokeVerification_ShouldFail_WhenNotVerified()
	{
		// Arrange
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		// Act
		var result = org.RevokeVerification();

		// Assert
		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Organization is not verified.");
		org.IsVerified.Should().BeFalse();
	}
}
