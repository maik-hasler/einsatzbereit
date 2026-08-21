using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace VisualTests;

// The three signed-in branches of the hero CTA - no organizations yet, one
// already organized, and the org-list fetch having failed - moved to
// frontend/src/pages/HomePage.test.tsx in einsatzbereit#2148. Each was a
// question about which branch HomePage renders for a given org list, and
// pinning that list was the expensive part: this class used to call
// fixture.ResetAsync() before every test and carry a keyed [NotInParallel]
// ("visualtests-db") purely so vera would deterministically organize
// nothing. A mocked return value answers the same question, so both the
// reset and the serialization are gone with them.
//
// This one stays because its assertion is about Keycloak: the CTA must land
// on the real /registrations endpoint, not a plain login.
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HomePageOrgCtaTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task Anonymous_HeroOrgCta_RedirectsToKeycloakRegistrationEndpoint()
	{
		// #693: the hero's second CTA is labelled "Create an organization" - it must
		// behave like the header's "Register" button (registration, not a plain login),
		// and it must stay visible for anonymous visitors (this is the case here).
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.AllowKeycloakCrossOriginRequestsAsync(Page);

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Create an organization" })
			.First.ClickAsync();

		await Expect(Page).ToHaveURLAsync(
			new Regex(@"/realms/einsatzbereit/protocol/openid-connect/registrations"));
		await Expect(Page.Locator("#kc-register-form")).ToBeVisibleAsync(new() { Timeout = 30_000 });
	}
}
