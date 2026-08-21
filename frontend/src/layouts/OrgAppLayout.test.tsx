import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import OrgAppLayout from "./OrgAppLayout";
import { renderWithProviders } from "../test/render";

/**
 * Was six of `OrgAppLayoutErrorStatesTests`' seven cases (#1224, #1774,
 * #1901, #2065), moved down in #2148 wave 2.
 *
 * #1224: the layout funnelled every org-load failure - a 403, a 404, a
 * dropped connection, a 500 - through one .catch() into a single "You are not
 * authorized" screen. #1774 found the branching still collapsed one case,
 * because the .catch() kept only the message string and threw the raw error
 * away, so a status NSwag generates no client branch for (the 400 an all-zero
 * organization id produces) fell through to the generic screen. The status is
 * what the branch reads now - which makes every one of these a question about
 * one rejected promise.
 *
 * `NonOrganizerVisitingOrgApp_Gets403_ShowsNotAuthorizedScreen` stays
 * end-to-end: it is the one case that also proves the *backend* answers 403
 * for a non-member, rather than only that the layout renders the right screen
 * when handed one.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

function rejectWith(status: number) {
	return Object.assign(new Error(`HTTP ${status}`), { status });
}

function setOnline(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

function renderOrgApp() {
	return renderWithProviders(
		<Routes>
			<Route path="/app/:organizationId" element={<OrgAppLayout />}>
				<Route path="dashboard" element={<div>Dashboard body</div>} />
			</Route>
		</Routes>,
		{ route: `/app/${ORG_ID}/dashboard`, auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
	setOnline(true);
	document.title = "";
});

afterEach(() => setOnline(true));

describe("OrgAppLayout org-load failures", () => {
	it("says the organization does not exist on a 404", async () => {
		api.getOrganizationDetails.mockRejectedValue(rejectWith(404));
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "Organization not found" }),
		).toBeInTheDocument();
		// notFound is a permanent fact about the request, so no retry is offered.
		expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
		await waitFor(() =>
			expect(document.title).toBe("Organization not found | Einsatzbereit"),
		);
	});

	it("says the same for the 400 an all-zero organization id produces", async () => {
		// #1774: NSwag generates no client branch for this status, so it used
		// to fall through to the generic "something went wrong" screen.
		api.getOrganizationDetails.mockRejectedValue(rejectWith(400));
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "Organization not found" }),
		).toBeInTheDocument();
		expect(
			screen.queryByRole("heading", { name: "Something went wrong" }),
		).toBeNull();
	});

	it("offers a recoverable state on a 500, and recovers on retry", async () => {
		api.getOrganizationDetails.mockRejectedValueOnce(rejectWith(500));
		api.getOrganizationDetails.mockResolvedValue({
			id: ORG_ID,
			name: "Freiwillige Feuerwehr Kiel",
			members: [],
			requestingUserRole: "Organizer",
			membersUnavailable: false,
			createdOn: new Date(Date.UTC(2026, 0, 1)),
		});
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "Something went wrong" }),
		).toBeInTheDocument();
		expect(
			screen.queryByText("You don't have access to this organization."),
		).toBeNull();

		await userEvent.click(screen.getByRole("button", { name: "Try again" }));

		expect(await screen.findByText("Dashboard body")).toBeInTheDocument();
		expect(
			screen.queryByRole("heading", { name: "Something went wrong" }),
		).toBeNull();
	});

	it("says it is offline rather than 'something went wrong'", async () => {
		setOnline(false);
		api.getOrganizationDetails.mockRejectedValue(new Error("network"));
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "You are offline" }),
		).toBeInTheDocument();
		expect(screen.queryByText(/An unexpected error occurred/)).toBeNull();
		expect(
			screen.getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});

	it("still says offline when navigator.onLine misreports true", async () => {
		// #1901: `online` alone can read true right after a hard reload while
		// genuinely offline; a rejection with no HTTP response at all is the
		// stronger signal.
		setOnline(true);
		api.getOrganizationDetails.mockRejectedValue(new Error("network"));
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "You are offline" }),
		).toBeInTheDocument();
		expect(screen.queryByText(/An unexpected error occurred/)).toBeNull();
	});

	it("recovers from offline on the manual retry alone, with no online event", async () => {
		// #2065: the automatic recovery depends on the browser firing `online`,
		// which some captive portals never do even once the connection is back.
		setOnline(true);
		api.getOrganizationDetails.mockRejectedValueOnce(new Error("network"));
		api.getOrganizationDetails.mockResolvedValue({
			id: ORG_ID,
			name: "Freiwillige Feuerwehr Kiel",
			members: [],
			requestingUserRole: "Organizer",
			membersUnavailable: false,
			createdOn: new Date(Date.UTC(2026, 0, 1)),
		});
		renderOrgApp();

		await screen.findByRole("heading", { name: "You are offline" });
		await userEvent.click(screen.getByRole("button", { name: "Try again" }));

		expect(await screen.findByText("Dashboard body")).toBeInTheDocument();
		expect(
			screen.queryByRole("heading", { name: "You are offline" }),
		).toBeNull();
	});
});

/**
 * `OrgAppShellHeaderTests`, `OrgAppAchievementNotifierTests` and the heading
 * half of `OrgDashboardWidgetsTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * All three are about what the shell itself mounts, independent of which tab
 * is inside it - which is exactly what rendering the layout around a stub
 * child isolates, and what an E2E driving a real dashboard could not separate
 * from the dashboard's own markup.
 */
describe("OrgAppLayout shell", () => {
	beforeEach(() => {
		api.getOrganizationDetails.mockResolvedValue({
			id: ORG_ID,
			name: "Freiwillige Feuerwehr Kiel",
			members: [],
			requestingUserRole: "Organizer",
			membersUnavailable: false,
			createdOn: new Date(Date.UTC(2026, 0, 1)),
		});
		api.getMyAchievements.mockResolvedValue([]);
		// The Header's own org list, which the switcher renders from - left
		// unmocked it never resolves and the switcher stays a skeleton.
		api.getOrganizations.mockResolvedValue([
			{ id: ORG_ID, name: "Freiwillige Feuerwehr Kiel" },
		]);
	});

	it("names the page in the band, never the organization", async () => {
		// The organization is the eyebrow above the title, not the title: an
		// organizer inside the shell already knows which org they are in, and
		// what they need from an <h1> is which of its pages they are on.
		renderOrgApp();

		const heading = await screen.findByRole("heading", { level: 1 });
		expect(heading).toHaveTextContent("Dashboard");
		expect(heading).not.toHaveTextContent("Freiwillige Feuerwehr Kiel");
		// Exactly one - a second would make the page's identity ambiguous.
		expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
	});

	it("keeps the way back and the way across in the chrome", async () => {
		renderOrgApp();

		// The switcher, for moving between organizations - awaited, since it
		// renders a skeleton until the Header's own org list resolves.
		expect(
			await screen.findByRole("button", { name: "Switch organization" }),
		).toBeInTheDocument();
		// ...and the section rail, for moving between this one's pages.
		const rail = screen.getByRole("navigation", {
			name: "Organization sections",
		});
		expect(within(rail).getByTestId("org-tab-dashboard")).toHaveAttribute(
			"aria-current",
			"page",
		);
	});

	it("runs its own achievements check on entry", async () => {
		// #1034: the org app shell is a separate layout from AppLayout, so a
		// volunteer who went straight into it never had their achievements
		// polled - badges earned elsewhere silently never toasted.
		renderOrgApp();

		await waitFor(() => expect(api.getMyAchievements).toHaveBeenCalled());
	});
});
