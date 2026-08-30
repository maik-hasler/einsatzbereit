import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = vi.hoisted(() => ({
	api: {
		createVolunteerOpportunity: vi.fn(),
		updateVolunteerOpportunity: vi.fn(),
		createTimeSlot: vi.fn(),
		publishVolunteerOpportunity: vi.fn(),
		uploadOpportunityBanner: vi.fn(),
		getVolunteerOpportunityDetails: vi.fn(),
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

	async function gotoDetails() {
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
			await waitFor(() =>
				screen.getByRole("heading", { name: new RegExp(stepTitle, "i") }),
			);
		}
	}

	async function addSlot(start: string, end: string) {
		fireEvent.change(
			document.querySelector("#slot-start") as HTMLInputElement,
			{
				target: { value: start },
			},
		);
		fireEvent.change(document.querySelector("#slot-end") as HTMLInputElement, {
			target: { value: end },
		});
		await userEvent.click(screen.getByRole("button", { name: "Add" }));
	}

	it("has no violations once a time slot is rejected inline (#2325)", async () => {
		open();
		await gotoDetails();

		await addSlot("2020-01-05T10:00", "2020-01-05T12:00");

		expect(
			await screen.findByText("Start date must be in the future."),
		).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations while the list and the add form flag an overlap (#2325)", async () => {
		open();
		await gotoDetails();
		await addSlot("2026-09-10T10:00", "2026-09-10T12:00");
		await addSlot("2026-09-10T11:00", "2026-09-10T13:00");

		// A third overlapping range left in the boxes, so the pre-add hint and
		// the two flagged rows are on screen at the same time.
		fireEvent.change(
			document.querySelector("#slot-start") as HTMLInputElement,
			{
				target: { value: "2026-09-10T11:30" },
			},
		);
		fireEvent.change(document.querySelector("#slot-end") as HTMLInputElement, {
			target: { value: "2026-09-10T12:30" },
		});

		expect(
			await screen.findByText(
				"This overlaps a time slot already on the list - a volunteer could sign up for both.",
			),
		).toBeInTheDocument();
		expect(screen.getAllByText("Overlaps another time slot")).toHaveLength(2);
		await expectNoA11yViolations();
	});

	it("has no violations while a banner can be replaced or removed (#2325)", async () => {
		renderWithProviders(
			<CreateVolunteerOpportunityModal
				organizationId="org-1"
				initialOpportunity={
					{
						id: "existing-opp-id",
						organizationId: "org-1",
						titleDe: "Bestehende Chance",
						descriptionDe: "Beschreibung.",
						isRemote: true,
						occurrence: "OneTime",
						participationType: "ScheduledSlots",
						checkInMethod: "None",
						tags: [],
						timeSlots: [],
						bannerImageUrl: "https://storage.example/banner.jpg",
					} as unknown as Parameters<
						typeof CreateVolunteerOpportunityModal
					>[0]["initialOpportunity"]
				}
				onClose={() => {}}
				onSuccess={() => {}}
			/>,
			{ auth: { isAuthenticated: true } },
		);

		expect(await screen.findByLabelText("Replace")).toBeInTheDocument();
		await expectNoA11yViolations();
	});

	it("has no violations on every step of the wizard", async () => {
		open();

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
