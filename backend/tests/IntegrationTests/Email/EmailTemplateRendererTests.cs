using Application.Common.Email;
using AwesomeAssertions;
using Infrastructure.Email;

namespace IntegrationTests.Email;

// Reads the real embedded en.json/de.json resources - no DB/Testcontainers
// involved, so this runs like a plain unit test despite living in the
// IntegrationTests project (which has the Infrastructure.csproj reference
// Application.UnitTests deliberately doesn't, per Clean Architecture layering).
public class EmailTemplateRendererTests
{
	private readonly EmailTemplateRenderer _sut = new();

	[Test]
	[Arguments("de")]
	[Arguments("en")]
	public void Render_ShouldProduceNonEmptySubjectAndBody_ForEveryTemplateKind_InEveryLanguage(string language)
	{
		foreach (var kind in Enum.GetValues<EmailTemplateKind>())
		{
			var placeholders = new Dictionary<string, string>
			{
				["OpportunityTitle"] = "Beach Cleanup",
				["VolunteerName"] = "Vera",
				["OrganizerName"] = "Olaf",
				["DisplayName"] = "Vera",
				["StartFormatted"] = "Monday, 1. January 2027 at 10:00",
				["Reason"] = "Not enough sign-ups",
				["ReasonBlock"] = "",
			};

			var content = _sut.Render(kind, language, placeholders);

			content.Body.Should().NotBeNullOrWhiteSpace();
			content.Body.Should().NotContain("{", $"'{kind}' ({language}) left an unresolved placeholder in its body");
		}
	}

	[Test]
	public void Render_ShouldInterpolateEveryPlaceholder_IntoSubjectAndBody()
	{
		var content = _sut.Render(
			EmailTemplateKind.EngagementConfirmed,
			"en",
			new Dictionary<string, string>
			{
				["VolunteerName"] = "Vera",
				["OpportunityTitle"] = "Beach Cleanup",
			});

		content.Subject.Should().Be("Your engagement has been confirmed");
		content.Body.Should().Contain("Vera").And.Contain("Beach Cleanup");
	}

	[Test]
	public void Render_ShouldFallBackToGerman_WhenLanguageIsUnsupported()
	{
		var content = _sut.Render(
			EmailTemplateKind.EngagementConfirmed,
			"fr",
			new Dictionary<string, string> { ["VolunteerName"] = "Vera", ["OpportunityTitle"] = "Beach Cleanup" });

		content.Subject.Should().Be("Deine Teilnahme wurde bestätigt");
	}
}
