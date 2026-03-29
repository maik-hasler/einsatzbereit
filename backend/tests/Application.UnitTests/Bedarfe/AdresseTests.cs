using AwesomeAssertions;
using Domain.Bedarfe;
using Domain.Primitives;
using Xunit;

namespace Application.UnitTests.Bedarfe;

public class AdresseTests
{
    [Fact]
    public void Constructor_ShouldCreateAdresse_WithValidData()
    {
        // Act
        var adresse = new Adresse("Musterstraße", "42a", "12345", "Berlin");

        // Assert
        adresse.Strasse.Should().Be("Musterstraße");
        adresse.Hausnummer.Should().Be("42a");
        adresse.Plz.Should().Be("12345");
        adresse.Ort.Should().Be("Berlin");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ShouldThrow_WhenStrasseIsEmpty(string? strasse)
    {
        var act = () => new Adresse(strasse!, "1", "12345", "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("Straße darf nicht leer sein.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ShouldThrow_WhenHausnummerIsEmpty(string? hausnummer)
    {
        var act = () => new Adresse("Straße", hausnummer!, "12345", "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("Hausnummer darf nicht leer sein.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("1234")]
    [InlineData("123456")]
    public void Constructor_ShouldThrow_WhenPlzIsInvalid(string? plz)
    {
        var act = () => new Adresse("Straße", "1", plz!, "Berlin");

        act.Should().Throw<DomainException>()
            .WithMessage("PLZ muss genau 5 Ziffern haben.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_ShouldThrow_WhenOrtIsEmpty(string? ort)
    {
        var act = () => new Adresse("Straße", "1", "12345", ort!);

        act.Should().Throw<DomainException>()
            .WithMessage("Ort darf nicht leer sein.");
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForSameValues()
    {
        var adresse1 = new Adresse("Straße", "1", "12345", "Berlin");
        var adresse2 = new Adresse("Straße", "1", "12345", "Berlin");

        adresse1.Should().Be(adresse2);
    }
}
