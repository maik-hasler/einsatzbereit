import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * Replaces `CreateVolunteerOpportunityModal_HasNoSeriousA11yViolations` and
 * `CreateOpportunityModalViewportTests`' a11y half. The scan it replaces only
 * ever saw step 1, because reaching the later steps end-to-end means filling
 * the form through a real browser; here each step is one `Next` away.
 */
const { api } = vi.hoisted(() => ({
	api: {
		createVolunteerOpportunity: vi.fn(),
		updateVolunteerOpportunity: vi.fn(),
		createTimeSlot: vi.fn(),
		publishVolunteerOpportunity: vi.fn(),
		uploadOpportunityBanner: vi.fn(),
		getVolunteerOpportunityDetails: vi.fn(),
		// Prefills the location step from the organization's own address.
		getOrganizationDetails: vi.fn(),
	},
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	vi.clearAllMocks();
	api.getOrganizationDetails.mockResolvedValue({
		id: "org-1",
		name: "Freiwillige Feuerwehr Kiel",
		address: {
			street: "Strandweg",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
		},
	});
});

function open() {
	return renderWithProviders(
		<CreateVolunteerOpportunityModal
			organizationId="org-1"
			onClose={() => {}}
			onSuccess={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("CreateVolunteerOpportunityModal a11y", () => {
	it("has no violations on the first step", async () => {
		open();
		expect(screen.getByRole("dialog")).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations once a blocked Next has produced an inline error", async () => {
		open();
		await userEvent.click(screen.getByRole("button", { name: "Next" }));

		await waitFor(() =>
			expect(document.querySelector('[aria-invalid="true"]')).not.toBeNull(),
		);
		await expectNoA11yViolations();
	});

	it("has no violations on every step of the wizard", async () => {
		open();

		// Step 1 - Basics. Both content languages render a Title/Description
		// pair; only the German one is required, and it comes first.
		await userEvent.type(
			screen.getAllByRole("textbox", { name: /title/i })[0],
			"Strandreinigung",
		);
		await userEvent.type(
			screen.getAllByRole("textbox", { name: /description/i })[0],
			"Wir sammeln einen Vormittag lang Muell am Strand.",
		);

		for (const stepTitle of ["Location", "Format", "Details"]) {
			await userEvent.click(screen.getByRole("button", { name: "Next" }));
			const reached = await waitFor(() =>
				screen.getByRole("heading", { name: new RegExp(stepTitle, "i") }),
			);
			expect(reached).toBeInTheDocument();
			await expectNoA11yViolations();
		}
	});
});
