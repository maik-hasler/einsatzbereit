using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1797: required fields were marked three different ways -
/// an aria-hidden asterisk from the wizard's own RequiredMark, an asterisk
/// baked into the translation string with a literal space ("Name *", org
/// settings) and a spelled-out "(required)" suffix (sign-up + feedback
/// modals). The baked-in variant also made the org settings field announce
/// as "Name star", because a marker inside a translated string cannot be
/// aria-hidden.
///
/// These tests pin the single convention: one shared, aria-hidden asterisk
/// rendered by the component, an accessible name with no marker in it, and
/// one legend explaining the asterisk per form.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class RequiredFieldMarkerTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task OrgSettings_NameField_MarksRequiredWithoutPollutingTheAccessibleName()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		// The accessible name is the field name alone. Before the fix the
		// asterisk lived in the string, so this resolved to "Name *" and a
		// screen reader read the field as "Name star".
		await Expect(Page.GetByRole(AriaRole.Textbox, new() { Name = "Name", Exact = true }))
			.ToBeVisibleAsync();

		await AssertLabelCarriesRequiredMarkAsync("org-name", "Name");
		await AssertFormExplainsTheAsteriskAsync();
	}

	[Test]
	public async Task CreateOpportunityWizard_TitleField_UsesTheSameMarkerAsTheRestOfTheProduct()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" });
		await Expect(createBtn.First).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await createBtn.First.ClickAsync();
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync(new() { Timeout = 5_000 });

		await Expect(Page.GetByRole(AriaRole.Textbox, new() { Name = "Title", Exact = true }))
			.ToBeVisibleAsync();

		await AssertLabelCarriesRequiredMarkAsync("opportunity-title", "Title");
		await AssertFormExplainsTheAsteriskAsync();
	}

	/// <summary>
	/// The marker is present on screen, sits directly against the label with no
	/// space character in between, and is hidden from assistive technology (the
	/// control's own required/aria-required is the accessible half).
	/// </summary>
	private async Task AssertLabelCarriesRequiredMarkAsync(string fieldId, string labelText)
	{
		var label = Page.Locator($"label[for='{fieldId}']");
		await Expect(label).ToBeVisibleAsync();
		await Expect(label).ToHaveTextAsync($"{labelText}*");

		var mark = label.Locator("span[aria-hidden='true']");
		await Expect(mark).ToHaveCountAsync(1);
		await Expect(mark).ToHaveTextAsync("*");
	}

	/// <summary>
	/// An asterisk needs a legend, and exactly one per form - that is the cost
	/// of picking the compact marker over the spelled-out "(required)".
	/// </summary>
	private async Task AssertFormExplainsTheAsteriskAsync()
	{
		var legend = Page.GetByText("* Required field");
		await Expect(legend).ToHaveCountAsync(1);
		await Expect(legend).ToBeVisibleAsync();
	}
}
