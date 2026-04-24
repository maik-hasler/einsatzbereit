using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;


namespace Application.UnitTests.VolunteerOpportunities;

public class VolunteerOpportunityTests
{
    private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
    private static readonly Address TestAddress = new("Musterstraße", "1", "12345", "Berlin");

    [Test]
    public void Create_ShouldCreateVolunteerOpportunity_WithValidData()
    {
        // Act
        var opportunity = VolunteerOpportunity.Create(
            TestOrganizationId,
            "Helpers needed",
            "We need helpers for moving",
            false,
            TestAddress,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        opportunity.Title.Should().Be("Helpers needed");
        opportunity.Description.Should().Be("We need helpers for moving");
        opportunity.OrganizationId.Should().Be(TestOrganizationId);
        opportunity.IsRemote.Should().BeFalse();
        opportunity.Address.Should().Be(TestAddress);
        opportunity.Occurrence.Should().Be(Occurrence.OneTime);
        opportunity.ParticipationType.Should().Be(ParticipationType.Waitlist);
    }

    [Test]
    public void Create_ShouldCreateRemoteOpportunity()
    {
        // Act
        var opportunity = VolunteerOpportunity.Create(
            TestOrganizationId,
            "Remote help",
            "Online volunteering",
            true,
            null,
            Occurrence.Recurring,
            ParticipationType.IndividualContact);

        // Assert
        opportunity.IsRemote.Should().BeTrue();
        opportunity.Address.Should().BeNull();
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void Create_ShouldThrowDomainException_WhenTitleIsEmpty(string? title)
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            title!,
            "Description",
            false,
            TestAddress,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Title must not be empty.");
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments(null)]
    public void Create_ShouldThrowDomainException_WhenDescriptionIsEmpty(string? description)
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            "Title",
            description!,
            false,
            TestAddress,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Description must not be empty.");
    }

    [Test]
    public void Create_ShouldThrow_WhenNotRemoteAndAddressIsNull()
    {
        // Act
        var act = () => VolunteerOpportunity.Create(
            TestOrganizationId,
            "Title",
            "Description",
            false,
            null,
            Occurrence.OneTime,
            ParticipationType.Waitlist);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Address is required for non-remote opportunities.");
    }

    [Test]
    public void Create_ShouldSetOccurrenceRecurring()
    {
        // Act
        var opportunity = VolunteerOpportunity.Create(
            TestOrganizationId,
            "Regular help",
            "Every Saturday",
            false,
            TestAddress,
            Occurrence.Recurring,
            ParticipationType.IndividualContact);

        // Assert
        opportunity.Occurrence.Should().Be(Occurrence.Recurring);
        opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
    }
}
