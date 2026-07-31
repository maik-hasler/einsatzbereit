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

}
