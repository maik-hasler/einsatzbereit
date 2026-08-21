import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgSettingsPage from "./OrgSettingsPage";
import { useQuickActionsList } from "../../contexts/QuickActionsContext";
import { inputClass, labelClass } from "../../lib/formClasses";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

const { api } = vi.hoisted(() => ({
	api: {
		updateOrganization: vi.fn(),
		uploadOrganizationLogo: vi.fn(),
		deleteOrganizationLogo: vi.fn(),
		deleteOrganization: vi.fn(),
	},
}));

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const org: OrganizationDetailsResponse = {
	id: "11111111-1111-1111-1111-111111111111",
	name: "Freiwillige Feuerwehr Kiel",
	description: "Wir helfen, wo Hilfe gebraucht wird.",
	contactEmail: "kontakt@example.test",
	contactPhone: "+49 431 123456",
	website: "https://example.test",
	logoUrl: undefined,
	address: {
		street: "Strandweg",
		houseNumber: "1",
		zipCode: "24103",
		city: "Kiel",
	},
	createdOn: new Date(Date.UTC(2026, 0, 1)),
	members: [],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
};

function QuickActionBar() {
	const actions = useQuickActionsList();
	return (
		<div>
			{actions.map((action) => (
				<button
					key={action.key}
					type="button"
					onClick={action.onClick}
					disabled={action.disabled}
				>
					{action.label}
				</button>
			))}
		</div>
	);
}

function renderPage(isOrganizer = true) {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<>
						<QuickActionBar />
						<Outlet context={{ org, reloadOrg: () => {}, isOrganizer }} />
					</>
				}
			>
				<Route index element={<OrgSettingsPage />} />
			</Route>
		</Routes>,
		{ auth: { isAuthenticated: true } },
	);
}

async function enterEditMode() {
	await userEvent.click(screen.getByRole("button", { name: "Edit" }));
	await waitFor(() =>
		expect(document.querySelector("#org-name")).not.toBeNull(),
	);
}

beforeEach(() => {
	vi.clearAllMocks();
});

describe("OrgSettingsPage edit form", () => {
	it("marks the required name field without polluting its accessible name", async () => {
		renderPage();
		await enterEditMode();

		expect(screen.getByRole("textbox", { name: "Name" })).toBeInTheDocument();

		const label = document.querySelector("label[for='org-name']");
		expect(label).toHaveTextContent(/^Name\*$/);

		const marks = label?.querySelectorAll("span[aria-hidden='true']") ?? [];
		expect(marks).toHaveLength(1);
		expect(marks[0]).toHaveTextContent("*");
	});

	it("explains the asterisk exactly once per form", async () => {
		renderPage();
		await enterEditMode();

		const legends = Array.from(document.querySelectorAll("p")).filter(
			(p) => p.textContent?.trim() === "* Required field",
		);
		expect(legends).toHaveLength(1);
		expect(legends[0]).toHaveAttribute("aria-hidden", "true");
	});

	it("uses the shared input and label classes rather than page-local copies", async () => {
		renderPage();
		await enterEditMode();

		expect(document.querySelector("#org-name")).toHaveAttribute(
			"class",
			inputClass,
		);
		expect(document.querySelector("label[for='org-name']")).toHaveAttribute(
			"class",
			labelClass,
		);
		expect(document.querySelector("label[for='org-street']")).toHaveAttribute(
			"class",
			labelClass,
		);
	});
});

describe("OrgSettingsPage danger zone", () => {
	it("is headed by the action it performs, in sentence case matching its button", () => {
		renderPage();

		expect(
			screen.getByRole("heading", { name: "Delete organization" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("heading", { name: /Danger zone/i })).toBeNull();
		expect(
			screen.getByRole("button", { name: "Delete organization" }),
		).toBeInTheDocument();
	});
});

describe("OrgSettingsPage danger-zone hint", () => {
	function renderWithMembers(count: number) {
		const members = Array.from({ length: count }, (_, i) => ({
			userId: `u${i}`,
			username: `member${i}`,
			role: i === 0 ? "Organizer" : "Member",
		}));
		return renderWithProviders(
			<Routes>
				<Route
					element={
						<Outlet
							context={{
								org: { ...org, members },
								reloadOrg: () => {},
								isOrganizer: true,
							}}
						/>
					}
				>
					<Route index element={<OrgSettingsPage />} />
				</Route>
			</Routes>,
			{ auth: { isAuthenticated: true } },
		);
	}

	it("explains why deletion is available to a sole member", async () => {
		renderWithMembers(1);

		expect(
			await screen.findByText(
				"You are this organization's sole remaining member, so you can delete it.",
			),
		).toBeInTheDocument();
	});

	it("explains why it is not, once someone else is a member", async () => {
		renderWithMembers(2);

		expect(
			await screen.findByText(
				"Only the organization's sole remaining member can delete it. Remove other members first.",
			),
		).toBeInTheDocument();
	});
});

describe("OrgSettingsPage logo upload rejection", () => {
	it("names the violation instead of repeating the format hint", async () => {
		renderPage();
		await enterEditMode();

		const input = document.querySelector<HTMLInputElement>("#logo-upload");
		expect(input).not.toBeNull();

		await userEvent.upload(
			input as HTMLInputElement,
			// applyAccept: false - the input has an `accept` list that userEvent
			// honours by default, dropping the file before the change event and
			// leaving the guard under test unexercised.
			new File(["not an image"], "notes.txt", { type: "text/plain" }),
			{ applyAccept: false },
		);

		const error = await waitFor(() => {
			const el = document.querySelector("#logo-upload-error");
			expect(el).not.toBeNull();
			return el as HTMLElement;
		});
		expect(error.textContent).toContain("notes.txt");
		expect(error.textContent).toContain("not a supported image");
		expect(error.textContent).not.toBe(
			screen.getByTestId("logo-upload-hint").textContent,
		);
		expect(api.uploadOrganizationLogo).not.toHaveBeenCalled();
	});
});
