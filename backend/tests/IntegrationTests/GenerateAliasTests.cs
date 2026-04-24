using AwesomeAssertions;
using Infrastructure.Keycloak;

namespace IntegrationTests;

public class GenerateAliasTests
{
    [Test]
    [Arguments("Feuerwehr Musterstadt", "feuerwehr-musterstadt")]
    [Arguments("THW Ortsverband", "thw-ortsverband")]
    [Arguments("simple", "simple")]
    public void GenerateAlias_ShouldLowercaseAndReplaceSpaces(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Test]
    [Arguments("Ärztlicher Bereitschaftsdienst", "aerztlicher-bereitschaftsdienst")]
    [Arguments("Münchner Rotes Kreuz", "muenchner-rotes-kreuz")]
    [Arguments("Übungsleiterkurs", "uebungsleiterkurs")]
    [Arguments("Straßenrettung", "strassenrettung")]
    [Arguments("Österreichisches Rotes Kreuz", "oesterreichisches-rotes-kreuz")]
    public void GenerateAlias_ShouldTransliterateGermanCharacters(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Test]
    [Arguments("Org (Test)", "org-test")]
    [Arguments("Org & Co.", "org-co")]
    [Arguments("Org #1 - Best!", "org-1-best")]
    [Arguments("---leading---", "leading")]
    [Arguments("  spaced  out  ", "spaced-out")]
    public void GenerateAlias_ShouldStripSpecialCharsAndCollapseHyphens(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Test]
    [Arguments("Café résumé", "cafe-resume")]
    [Arguments("naïve exposé", "naive-expose")]
    public void GenerateAlias_ShouldStripDiacriticsFromNonGermanChars(string name, string expected)
    {
        KeycloakOrganizationService.GenerateAlias(name).Should().Be(expected);
    }

    [Test]
    public void GenerateAlias_ShouldReturnEmpty_WhenNameIsOnlySpecialChars()
    {
        KeycloakOrganizationService.GenerateAlias("@#$%").Should().BeEmpty();
    }

    [Test]
    public void GenerateAlias_ShouldHandleEmptyString()
    {
        KeycloakOrganizationService.GenerateAlias("").Should().BeEmpty();
    }
}
