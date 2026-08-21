import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgSettingsPage from "./OrgSettingsPage";
import { useQuickActionsList } from "../../contexts/QuickActionsContext";
import { inputClass, labelClass } from "../../lib/formClasses";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

/**
 * Was the org-settings halves of `RequiredFieldMarkerTests` (#1797),
 * `SharedFormClassesTests` (#536/#1104/#1109/#1673) and
 * `DangerZonePanelTests` (#1792), moved down in #2148 wave 2. All three were
 * class-attribute and label-markup assertions that each paid a login, a
 * dashboard navigation and an edit-mode click to reach.
 */
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

/**
 * Stands in for the org app header's action bar. The page registers its
 * Edit/Save/Cancel trio into QuickActionsContext (useEditModeQuickActions)
 * rather than rendering them itself, so an isolated render has no way into
 * edit mode without something consuming that registration.
 */
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
		// #1797: the asterisk used to live inside the translated string
		// ("Name *"), which cannot be aria-hidden - a screen reader read the
		// field as "Name star".
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
		// The cost of picking the compact marker over a spelled-out
		// "(required)": it needs a legend, and exactly one of them. The legend
		// is split across elements (an unhidden <span>*</span> plus text), so
		// match on the paragraph's own text rather than a text node's.
		renderPage();
		await enterEditMode();

		const legends = Array.from(document.querySelectorAll("p")).filter(
			(p) => p.textContent?.trim() === "* Required field",
		);
		expect(legends).toHaveLength(1);
		expect(legends[0]).toHaveAttribute("aria-hidden", "true");
	});

	it("uses the shared input and label classes rather than page-local copies", async () => {
		// #536/#1104/#1109/#1673: this page and the profile page each used to
		// define their own inputClass constant and their own Field helper with
		// its own label style - the org form even switched label styles partway
		// down, at the address block. Comparing against the imported constants
		// (rather than a hardcoded copy of them, as the Playwright original
		// did) means a deliberate change to the shared recipe does not have to
		// be re-typed here to keep the test honest.
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
		// The address block lower down must not switch styles.
		expect(document.querySelector("label[for='org-street']")).toHaveAttribute(
			"class",
			labelClass,
		);
	});
});

describe("OrgSettingsPage danger zone", () => {
	it("is headed by the action it performs, in sentence case matching its button", () => {
		// #1792: the panel was headed "Danger zone"/"Gefahrenzone", and the
		// heading and the button below it disagreed on casing.
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

/**
 * The danger-zone hint case from `OrganizationTests` and
 * `LogoUploadRejectionMessageTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 */
describe("OrgSettingsPage danger-zone hint", () => {
	/** Renders the page for an organization with exactly `count` members. */
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
		// The hint branches on the same member count the delete button's
		// `disabled` already did - which is the point: a disabled button with no
		// reason beside it tells a keyboard or screen-reader user nothing.
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
		// The hint ("JPEG, PNG or WebP, max. ...") is always on screen, so
		// echoing it as the error told the organizer nothing about what went
		// wrong with the file they actually picked.
		renderPage();
		await enterEditMode();

		const input = document.querySelector<HTMLInputElement>("#logo-upload");
		expect(input).not.toBeNull();

		// jsdom reads only `type` and `size` off a File, which is exactly what
		// `validateImageUpload` inspects - so the rejection path is identical
		// here to a real picked file.
		// `applyAccept: false` because the input carries an `accept` list and
		// userEvent honours it by default - which would drop the file before the
		// change event, leaving the client-side guard under test unexercised. A
		// real file picker's accept filter is a convenience, not the guard.
		await userEvent.upload(
			input as HTMLInputElement,
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
		// And it is distinct from the hint, rather than a copy of it.
		expect(error.textContent).not.toBe(
			screen.getByTestId("logo-upload-hint").textContent,
		);
		expect(api.uploadOrganizationLogo).not.toHaveBeenCalled();
	});
});
