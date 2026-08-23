using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;

namespace Application.UnitTests.Organizations;

public class OrganizationTests
{
	[Test]
	public void Update_ShouldSetAllFields()
	{
		var org = Organization.Create(OrganizationId.New(), "Original").Value;
		var address = Address.Create("Sample Street", "1", "12345", "Berlin").Value;

		org.Rename("Updated");
		org.ChangeDescription("Description");
		org.ChangeContactInfo("mail@test.de", "+49 30 123", "https://test.de");
		org.Relocate(address);

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
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		org.Relocate(Address.Create("St", "1", "12345", "City").Value);

		org.Relocate(null);

		org.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Rename_ShouldFail_WhenNameIsEmpty(string? name)
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		var result = org.Rename(name!);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Name must not be empty.");
	}

	[Test]
	public void Create_ShouldFail_WhenNameIsEmpty()
	{
		var result = Organization.Create(OrganizationId.New(), "");

		result.IsFailure.Should().BeTrue();
	}

	// --- Name length cap (#1158) ---
	// Create already enforced this in CreateOrganizationCommandHandler (before its
	// Keycloak call), but Rename had no cap at all - both now share the same rule.

	[Test]
	public void Create_ShouldFail_WhenNameExceedsMaxLength()
	{
		var result = Organization.Create(OrganizationId.New(), new string('a', Organization.MaxNameLength + 1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Contain("100 characters");
	}

	[Test]
	public void Create_ShouldSucceed_WhenNameIsExactlyMaxLength()
	{
		var result = Organization.Create(OrganizationId.New(), new string('a', Organization.MaxNameLength));

		result.IsFailure.Should().BeFalse();
	}

	[Test]
	public void Rename_ShouldFail_WhenNameExceedsMaxLength()
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		var result = org.Rename(new string('a', Organization.MaxNameLength + 1));

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Contain("100 characters");
		org.Name.Should().Be("Org");
	}

	[Test]
	public void Rename_ShouldSucceed_WhenNameIsExactlyMaxLength()
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		var maxLengthName = new string('a', Organization.MaxNameLength);

		var result = org.Rename(maxLengthName);

		result.IsFailure.Should().BeFalse();
		org.Name.Should().Be(maxLengthName);
	}

	[Test]
	[Arguments(null)]
	[Arguments("")]
	[Arguments("   ")]
	public void ChangeContactInfo_ShouldSucceed_WhenWebsiteIsNotProvided(string? website)
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		var result = org.ChangeContactInfo("mail@test.de", "+49 30 123", website);

		result.IsSuccess.Should().BeTrue();
		org.Website.Should().Be(website);
	}

	[Test]
	[Arguments("not-a-url")]
	[Arguments("javascript:alert(1)")]
	[Arguments("ftp://test.de")]
	[Arguments("//test.de")]
	public void ChangeContactInfo_ShouldFail_WhenWebsiteIsNotAnHttpOrHttpsUrl(string website)
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;

		var result = org.ChangeContactInfo(null, null, website);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Website must be a valid http or https URL.");
		org.Website.Should().BeNull();
	}

	[Test]
	public void ChangeContactInfo_ShouldFail_WhenWebsiteExceedsMaxLength()
	{
		var org = Organization.Create(OrganizationId.New(), "Org").Value;
		var website = "https://test.de/" + new string('a', 500);

		var result = org.ChangeContactInfo(null, null, website);

		result.IsFailure.Should().BeTrue();
		result.Error.Description.Should().Be("Website must not exceed 500 characters.");
		org.Website.Should().BeNull();
	}
}
