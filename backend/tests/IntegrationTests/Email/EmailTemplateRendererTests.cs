using Application.Common.Email;
using AwesomeAssertions;
using Infrastructure.Email;

namespace IntegrationTests.Email;

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
				["InviteeName"] = "Vera",
				["OrganizationName"] = "Beach Cleanup Crew",
				["Count"] = "2",
				["OpportunitiesList"] = "- Beach Cleanup\n- Park Cleanup",
				["ItemsList"] = "- Vera signed up for \"Beach Cleanup\"",
				["UnsubscribeUrl"] = "https://example.com/unsubscribe",
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
	public void Render_ShouldInterpolateEveryPlaceholder_ForInvitationReceived_InGerman()
	{
		var content = _sut.Render(
			EmailTemplateKind.InvitationReceived,
			"de",
			new Dictionary<string, string> { ["InviteeName"] = "Vera", ["OrganizationName"] = "Strandreinigung e.V." });

		content.Subject.Should().Be("Du wurdest eingeladen, einer Organisation beizutreten");
		content.Body.Should().Contain("Vera").And.Contain("Strandreinigung e.V.");
	}

	[Test]
	public void Render_ShouldInterpolateEveryPlaceholder_ForOpportunityUpdated_InEnglish()
	{
		var content = _sut.Render(
			EmailTemplateKind.OpportunityUpdated,
			"en",
			new Dictionary<string, string> { ["VolunteerName"] = "Vera", ["OpportunityTitle"] = "Beach Cleanup" });

		content.Subject.Should().Contain("Beach Cleanup");
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
