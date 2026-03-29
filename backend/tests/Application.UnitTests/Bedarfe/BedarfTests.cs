using AwesomeAssertions;
using Domain.Bedarfe;
using Domain.Organisationen;
using Domain.Primitives;
using Xunit;

namespace Application.UnitTests.Bedarfe;

public class BedarfTests
{
    private static readonly OrganisationId TestOrganisationId = new(Guid.NewGuid());
    private static readonly Adresse TestAdresse = new("Musterstraße", "1", "12345", "Berlin");

    [Fact]
    public void Create_ShouldCreateDraftBedarf_WithValidData()
    {
        // Act
        var bedarf = Bedarf.Create(
            "Helfer gesucht",
            "Wir suchen Helfer für den Umzug",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);

        // Assert
        bedarf.Title.Should().Be("Helfer gesucht");
        bedarf.Description.Should().Be("Wir suchen Helfer für den Umzug");
        bedarf.OrganisationId.Should().Be(TestOrganisationId);
        bedarf.Adresse.Should().Be(TestAdresse);
        bedarf.Frequenz.Should().Be(Frequenz.Einmalig);
        bedarf.Status.Should().BeOfType<Draft>();
        bedarf.PublishedOn.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrowDomainException_WhenTitleIsEmpty(string? title)
    {
        // Act
        var act = () => Bedarf.Create(
            title!,
            "Beschreibung",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Titel darf nicht leer sein.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_ShouldThrowDomainException_WhenDescriptionIsEmpty(string? description)
    {
        // Act
        var act = () => Bedarf.Create(
            "Titel",
            description!,
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Beschreibung darf nicht leer sein.");
    }

    [Fact]
    public void Create_ShouldThrow_WhenAdresseIsNull()
    {
        // Act
        var act = () => Bedarf.Create(
            "Titel",
            "Beschreibung",
            TestOrganisationId,
            null!,
            Frequenz.Einmalig);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_ShouldSetFrequenzRegelmaessig()
    {
        // Act
        var bedarf = Bedarf.Create(
            "Regelmäßige Hilfe",
            "Jeden Samstag",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Regelmaessig);

        // Assert
        bedarf.Frequenz.Should().Be(Frequenz.Regelmaessig);
    }

    [Fact]
    public void Publish_ShouldSetPublishedStatus_WhenDraft()
    {
        // Arrange
        var bedarf = Bedarf.Create(
            "Titel",
            "Beschreibung",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);
        var publishedOn = DateTimeOffset.UtcNow;

        // Act
        bedarf.Publish(publishedOn);

        // Assert
        bedarf.Status.Should().BeOfType<Published>();
        bedarf.PublishedOn.Should().Be(publishedOn);
    }

    [Fact]
    public void Publish_ShouldThrowDomainException_WhenAlreadyPublished()
    {
        // Arrange
        var bedarf = Bedarf.Create(
            "Titel",
            "Beschreibung",
            TestOrganisationId,
            TestAdresse,
            Frequenz.Einmalig);
        bedarf.Publish(DateTimeOffset.UtcNow);

        // Act
        var act = () => bedarf.Publish(DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("Bereits veröffentlicht.");
    }
}
