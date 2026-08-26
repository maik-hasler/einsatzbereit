import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import OrgAppLayout from "./OrgAppLayout";
import { renderWithProviders } from "../test/render";

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
		expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
		await waitFor(() =>
			expect(document.title).toBe("Organization not found | Einsatzbereit"),
		);
	});

	it("says the same for the 400 an all-zero organization id produces", async () => {
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
		setOnline(true);
		api.getOrganizationDetails.mockRejectedValue(new Error("network"));
		renderOrgApp();

		expect(
			await screen.findByRole("heading", { name: "You are offline" }),
		).toBeInTheDocument();
		expect(screen.queryByText(/An unexpected error occurred/)).toBeNull();
	});

	it("recovers from offline on the manual retry alone, with no online event", async () => {
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
		api.getOrganizations.mockResolvedValue([
			{ id: ORG_ID, name: "Freiwillige Feuerwehr Kiel" },
		]);
	});

	it("names the page in the band, never the organization", async () => {
		renderOrgApp();

		const heading = await screen.findByRole("heading", { level: 1 });
		expect(heading).toHaveTextContent("Dashboard");
		expect(heading).not.toHaveTextContent("Freiwillige Feuerwehr Kiel");
		expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);
	});

	it("keeps the way back and the way across in the chrome", async () => {
		renderOrgApp();

		expect(
			await screen.findByRole("button", {
				name: "Switch organization, currently Freiwillige Feuerwehr Kiel",
			}),
		).toBeInTheDocument();
		const rail = screen.getByRole("navigation", {
			name: "Organization sections",
		});
		expect(within(rail).getByTestId("org-tab-dashboard")).toHaveAttribute(
			"aria-current",
			"page",
		);
	});

	it("runs its own achievements check on entry", async () => {
		renderOrgApp();

		await waitFor(() => expect(api.getMyAchievements).toHaveBeenCalled());
	});
});
