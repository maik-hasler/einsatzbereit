import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import HomePage from "./HomePage";
import { useLocation } from "react-router";
import { renderWithProviders } from "../test/render";

/** Reads back where a navigation landed, without a real browser URL bar. */
function LocationProbe() {
	const location = useLocation();
	return (
		<span data-testid="location-probe">{`${location.pathname}${location.search}`}</span>
	);
}

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
		searchCities: vi.fn(),
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

/**
 * `NavigationTests`' landing-page markup cases and `HeaderPrimaryNavTests`'
 * hero-search case, moved down in #2148 wave 13. Remaining inventory: #2159.
 */
describe("HomePage structure", () => {
	it("leads with a main heading", async () => {
		renderWithProviders(<HomePage />);

		const heading = await screen.findByRole("heading", { level: 1 });
		expect(heading.textContent?.trim()).not.toBe("");
	});

	it("carries no breadcrumb, being the root of every trail", async () => {
		// The positive half first: a page that rendered nothing at all would
		// satisfy the absence on its own.
		renderWithProviders(<HomePage />);

		await screen.findByRole("heading", { level: 1 });
		expect(document.querySelector('nav[aria-label="Breadcrumb"]')).toBeNull();
	});
});

describe("HomePage hero search", () => {
	it("carries its keyword into the browse list", async () => {
		// With the location field left empty, `handleHeroSearch` skips
		// `searchCities` entirely and builds the query from the keyword alone -
		// which is what makes this a client-state case rather than a geocoding
		// one. The E2E drove a real navigation to read the resulting URL.
		renderWithProviders(
			<>
				<HomePage />
				<LocationProbe />
			</>,
		);

		await userEvent.type(
			screen.getByTestId("hero-keyword-input"),
			"Erste Hilfe",
		);
		await userEvent.click(screen.getByRole("button", { name: /Search/i }));

		// URLSearchParams encodes a space as "+", not "%20" - so this is the
		// literal string the browse route receives.
		const probe = await screen.findByTestId("location-probe");
		expect(probe.textContent).toBe("/opportunities?q=Erste+Hilfe");
		// No location was typed, so nothing was geocoded.
		expect(api.searchCities).not.toHaveBeenCalled();
	});
});
