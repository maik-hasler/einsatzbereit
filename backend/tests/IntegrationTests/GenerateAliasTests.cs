using AwesomeAssertions;
using Infrastructure.Keycloak;
using Xunit;

namespace IntegrationTests;

public class GenerateAliasTests
{
    [Theory]
    [InlineData("Feuerwehr Musterstadt", "feuerwehr-musterstadt")]
    [InlineData("THW Ortsverband", "thw-ortsverband")]
    [InlineData("simple", "simple")]
    public void GenerateAlias_ShouldLowercaseAndReplaceSpaces(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Ärztlicher Bereitschaftsdienst", "aerztlicher-bereitschaftsdienst")]
    [InlineData("Münchner Rotes Kreuz", "muenchner-rotes-kreuz")]
    [InlineData("Übungsleiterkurs", "uebungsleiterkurs")]
    [InlineData("Straßenrettung", "strassenrettung")]
    [InlineData("Österreichisches Rotes Kreuz", "oesterreichisches-rotes-kreuz")]
    public void GenerateAlias_ShouldTransliterateGermanCharacters(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Org (Test)", "org-test")]
    [InlineData("Org & Co.", "org-co")]
    [InlineData("Org #1 - Best!", "org-1-best")]
    [InlineData("---leading---", "leading")]
    [InlineData("  spaced  out  ", "spaced-out")]
    public void GenerateAlias_ShouldStripSpecialCharsAndCollapseHyphens(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("Café résumé", "cafe-resume")]
    [InlineData("naïve exposé", "naive-expose")]
    public void GenerateAlias_ShouldStripDiacriticsFromNonGermanChars(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Fact]
    public void GenerateAlias_ShouldReturnEmpty_WhenNameIsOnlySpecialChars()
    {
        KeycloakOrganizationService.GenerateAlias("@#$%").Should().BeEmpty();
    }

    [Fact]
    public void GenerateAlias_ShouldHandleEmptyString()
    {
        KeycloakOrganizationService.GenerateAlias("").Should().BeEmpty();
    }
}
