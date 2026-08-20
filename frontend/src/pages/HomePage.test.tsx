import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import HomePage from "./HomePage";
import { renderWithProviders } from "../test/render";

/**
 * Was most of `HomePageOrgCtaTests` (#693, #1316) plus the landing half of
 * `HelpContactImprintContentTests`' FAQ pairing (#2061), moved down in #2148
 * wave 2.
 *
 * `HomePageOrgCtaTests` was the most expensive shape in the suite: it called
 * `fixture.ResetAsync()` before every test and carried a keyed
 * `[NotInParallel]` so it could guarantee vera organizes nothing. Here that
 * guarantee is a mocked return value, and the class it came from no longer
 * needs the database reset or the serialization at all.
 *
 * What stayed end-to-end is the anonymous CTA, because its assertion is that
 * the click lands on Keycloak's real `/registrations` endpoint.
 */
const { api } = vi.hoisted(() => ({
	api: {
		getOrganizations: vi.fn(),
		getVolunteerOpportunities: vi.fn(),
		createOrganization: vi.fn(),
		uploadOrganizationLogo: vi.fn(),
	},
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const org = {
	id: "11111111-1111-1111-1111-111111111111",
	name: "Freiwillige Feuerwehr Kiel",
	logoUrl: undefined,
};

beforeEach(() => {
	vi.clearAllMocks();
	// useSharedOrgFetch keys its in-flight registry by resource name at module
	// scope, so tests must leave every promise settled; a pending one would
	// still be registered when the next test mounts.
	api.getOrganizations.mockResolvedValue([]);
	api.getVolunteerOpportunities.mockResolvedValue({
		items: [],
		hasMore: false,
	});
	localStorage.clear();
	document.cookie = "active-org=; Max-Age=0; path=/";
});

describe("HomePage hero organization CTA", () => {
	it("opens organization creation in place for a signed-in visitor with no organizations", async () => {
		renderWithProviders(<HomePage />, { auth: { isAuthenticated: true } });

		const cta = await screen.findByRole("button", {
			name: "Create an organization",
		});
		await userEvent.click(cta);

		// Lazy-loaded chunk behind a Suspense fallback, so the first dialog to
		// appear is ModalLoadingFallback's ("Loading...") - name the one being
		// waited for rather than the first one that shows up.
		const dialog = await screen.findByRole("dialog", {
			name: /Create organization/,
		});
		expect(dialog).toBeInTheDocument();

		// "In place": the landing page is still mounted underneath, so the
		// click opened a dialog rather than navigating to a creation route.
		expect(screen.getByRole("heading", { level: 1 })).toBeInTheDocument();
	});

	it("swaps the CTA for an organization-overview link once the visitor organizes one", async () => {
		api.getOrganizations.mockResolvedValue([org]);
		renderWithProviders(<HomePage />, { auth: { isAuthenticated: true } });

		const link = await screen.findByRole("link", {
			name: "Organization overview",
		});
		expect(link).toHaveAttribute("href", `/app/${org.id}/dashboard`);
		expect(
			screen.queryByRole("button", { name: "Create an organization" }),
		).toBeNull();
	});

	it("shows neither branch while the organization list failed to load", async () => {
		// HomePage used to destructure only useSharedOrgFetch's data slot, so
		// "still loading" and "fetch failed" collapsed into the same empty-array
		// state as a genuine zero-organization user - and an organizer whose
		// fetch failed was offered a "create an organization" button that would
		// have duplicated an organization they already had. Asserts the
		// contract, not a particular recovery mechanism.
		api.getOrganizations.mockRejectedValue(new Error("network"));
		renderWithProviders(<HomePage />, { auth: { isAuthenticated: true } });

		await screen.findByRole("heading", { level: 1 });
		await vi.waitFor(() => {
			expect(api.getOrganizations).toHaveBeenCalled();
		});

		expect(
			screen.queryByRole("button", { name: "Create an organization" }),
		).toBeNull();
		expect(
			screen.queryByRole("link", { name: "Organization overview" }),
		).toBeNull();
	});
});

describe("HomePage FAQ", () => {
	it("draws its questions from the ones /help answers", async () => {
		// #2061: the landing FAQ used to answer four questions the Help Center
		// never covered, breaking the "More questions? See Help" link's whole
		// premise. HelpPage.test.tsx asserts the other half of this pairing.
		renderWithProviders(<HomePage />);

		expect(
			await screen.findByText("Does using Einsatzbereit cost anything?"),
		).toBeInTheDocument();
		expect(screen.getByRole("link", { name: /Help/ })).toBeInTheDocument();
	});
});
