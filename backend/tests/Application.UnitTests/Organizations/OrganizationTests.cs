using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;


namespace Application.UnitTests.Organizations;

public class OrganizationTests
{
	[Test]
	public void Update_ShouldSetAllFields()
	{
		// Arrange
		var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Original");
		var address = new Address("Sample Street", "1", "12345", "Berlin");

		// Act
		org.Update("Updated", "Description", "mail@test.de", "+49 30 123", "https://test.de", address);

		// Assert
		org.Name.Should().Be("Updated");
		org.Description.Should().Be("Description");
		org.ContactEmail.Should().Be("mail@test.de");
		org.ContactPhone.Should().Be("+49 30 123");
		org.Website.Should().Be("https://test.de");
		org.Address.Should().Be(address);
	}

	[Test]
	public void Update_ShouldClearAddress_WhenNullPassed()
	{
		// Arrange
		var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Org");
		org.Update("Org", null, null, null, null, new Address("St", "1", "12345", "City"));

		// Act
		org.Update("Org", null, null, null, null, null);

		// Assert
		org.Address.Should().BeNull();
	}

	[Test]
	[Arguments("")]
	[Arguments("   ")]
	[Arguments(null)]
	public void Update_ShouldThrow_WhenNameIsEmpty(string? name)
	{
		// Arrange
		var org = Organization.Create(new OrganizationId(Guid.NewGuid()), "Org");

		// Act
		var act = () => org.Update(name!, null, null, null, null, null);

		// Assert
		act.Should().Throw<DomainException>()
			.WithMessage("Name must not be empty.");
	}

	[Test]
	public void Create_ShouldThrow_WhenNameIsEmpty()
	{
		var act = () => Organization.Create(new OrganizationId(Guid.NewGuid()), "");
		act.Should().Throw<DomainException>();
	}
}
