using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Xunit;

namespace Application.UnitTests.VolunteerOpportunities;

public class VolunteerOpportunityTests
{
    private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
    private static readonly Location TestLocation = new PhysicalLocation(new Address("Musterstraße", "1", "12345", "Berlin"));

    [Fact]
    public void Create_ShouldCreateVolunteerOpportunity_WithValidData()
    {
        // Act
        var opportunity = VolunteerOpportunity.Create(
            TestOrganizationId,
            "Helpers needed",
            "We need helpers for moving",
            TestLocation,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        opportunity.Title.Should().Be("Helpers needed");
        opportunity.Description.Should().Be("We need helpers for moving");
        opportunity.OrganizationId.Should().Be(TestOrganizationId);
        opportunity.Location.Should().Be(TestLocation);
        opportunity.Occurrence.Should().Be(Occurrence.OneTime);
        opportunity.ParticipationType.Should().Be(ParticipationType.Waitlist);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrowDomainException_WhenTitleIsEmpty(string? title)
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            title!,
            "Description",
            TestLocation,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Title must not be empty.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrowDomainException_WhenDescriptionIsEmpty(string? description)
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            "Title",
            description!,
            TestLocation,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Description must not be empty.");
    }

    [Fact]
    public void Create_ShouldThrow_WhenLocationIsNull()
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            "Title",
            "Description",
            null!,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ShouldSetOccurrenceRecurring()
    {
        // Act
        var opportunity = VolunteerOpportunity.Create(
            TestOrganizationId,
            "Regular help",
            "Every Saturday",
            TestLocation,
            Occurrence.Recurring,
            ParticipationType.IndividualContact);

        // Assert
        opportunity.Occurrence.Should().Be(Occurrence.Recurring);
        opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
    }
}
