import { describe, it, expect, vi, beforeEach } from "vitest";
import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import { renderWithProviders } from "../test/render";

const { api } = vi.hoisted(() => ({
	api: {
		getOrganizationDetails: vi.fn(),
		createVolunteerOpportunity: vi.fn(),
		updateVolunteerOpportunity: vi.fn(),
		createTimeSlot: vi.fn(),
		publishVolunteerOpportunity: vi.fn(),
		uploadOpportunityBanner: vi.fn(),
		getVolunteerOpportunityDetails: vi.fn(),
		getOpportunityCheckInPin: vi.fn(),
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

function openWizard() {
	return renderWithProviders(
		<CreateVolunteerOpportunityModal
			organizationId="org-1"
			onClose={() => {}}
			onSuccess={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

const title = () => document.querySelector("#opportunity-title") as HTMLElement;
const description = () =>
	document.querySelector("#opportunity-description") as HTMLElement;
const titleError = () => document.querySelector("#opportunity-title-error");
const descriptionError = () =>
	document.querySelector("#opportunity-description-error");
const blocked = () =>
	document.querySelector("#create-opportunity-step-blocked");

describe("create-opportunity wizard: focus on the first invalid field (#2077)", () => {
	it("focuses the title when both required fields are blank", async () => {
		openWizard();
		await userEvent.click(screen.getByTestId("modal-next"));

		await waitFor(() =>
			expect(titleError()).toHaveTextContent(
				"Enter a title - it appears in search.",
			),
		);
		expect(title()).toHaveFocus();
	});

	it("focuses the description when only it is blank, not the title", async () => {
		openWizard();
		await userEvent.type(title(), "Focus regression test");
		await userEvent.click(screen.getByTestId("modal-next"));

		await waitFor(() =>
			expect(descriptionError()).toHaveTextContent(
				"Describe briefly what volunteers can expect.",
			),
		);
		expect(description()).toHaveFocus();
		expect(titleError()).toBeNull();
	});

	it("focuses the blocking field when a stepper jump is refused", async () => {
		openWizard();
		await userEvent.click(screen.getByTestId("wizard-stepper-4"));

		await waitFor(() => expect(blocked()).not.toBeNull());
		expect(screen.getByTestId("wizard-step-1")).toBeInTheDocument();
		expect(title()).toHaveFocus();
	});
});

describe("create-opportunity wizard: blocked stepper jumps (#1782)", () => {
	it("names the step standing in the way, in an assertive live region tied to the refused control", async () => {
		openWizard();
		await userEvent.click(screen.getByTestId("wizard-stepper-4"));

		await waitFor(() => expect(blocked()).not.toBeNull());
		const message = blocked();
		expect(message).toHaveAttribute("role", "alert");
		expect(message).toHaveAttribute("aria-live", "assertive");
		expect(message).toHaveTextContent("Step 4 is not available yet");
		expect(message).toHaveTextContent("step 1 (Basics)");

		expect(screen.getByTestId("wizard-stepper-4")).toHaveAttribute(
			"aria-describedby",
			"create-opportunity-step-blocked",
		);

		expect(screen.getByTestId("wizard-step-1")).toBeInTheDocument();
		expect(screen.queryByTestId("wizard-step-4")).toBeNull();
	});

	it("retires the refusal once the named step is fixed, and lets the same click through", async () => {
		openWizard();
		await userEvent.click(screen.getByTestId("wizard-stepper-4"));
		await waitFor(() => expect(blocked()).not.toBeNull());

		await userEvent.type(title(), "Blocked step jump regression");
		await userEvent.type(description(), "Regression test for #1782.");
		await userEvent.tab();

		await waitFor(() => expect(blocked()).toBeNull());
		expect(screen.getByTestId("wizard-stepper-4")).not.toHaveAttribute(
			"aria-describedby",
			"create-opportunity-step-blocked",
		);

		await userEvent.click(screen.getByTestId("wizard-stepper-4"));
		await waitFor(() =>
			expect(screen.getByTestId("wizard-step-4")).toBeInTheDocument(),
		);
		expect(blocked()).toBeNull();
	});

	it("names an intermediate step rather than the one being looked at", async () => {
		openWizard();
		await userEvent.type(title(), "Intermediate step block");
		await userEvent.type(description(), "Regression test for #1782.");

		await userEvent.click(screen.getByTestId("wizard-stepper-2"));
		await waitFor(() =>
			expect(screen.getByTestId("wizard-step-2")).toBeInTheDocument(),
		);
		const city = document.querySelector("#opportunity-city") as HTMLElement;
		await userEvent.clear(city);

		await userEvent.click(screen.getByTestId("wizard-stepper-1"));
		await waitFor(() =>
			expect(screen.getByTestId("wizard-step-1")).toBeInTheDocument(),
		);
		await userEvent.click(screen.getByTestId("wizard-stepper-4"));

		await waitFor(() =>
			expect(blocked()).toHaveTextContent("step 2 (Location)"),
		);
		expect(screen.getByTestId("wizard-step-1")).toBeInTheDocument();
	});
});

describe("create-opportunity wizard: live revalidation (#1928)", () => {
	it("clears a field's error as it is fixed, without a second Next click", async () => {
		openWizard();
		await userEvent.click(screen.getByTestId("modal-next"));

		await waitFor(() =>
			expect(titleError()).toHaveTextContent(
				"Enter a title - it appears in search.",
			),
		);
		expect(descriptionError()).toHaveTextContent(
			"Describe briefly what volunteers can expect.",
		);
		expect(title()).toHaveAttribute("aria-invalid", "true");

		await userEvent.type(title(), "Erste-Hilfe-Kurs fuer Anfaenger");

		await waitFor(() => expect(titleError()).toBeNull());
		expect(title()).not.toHaveAttribute("aria-invalid");

		expect(descriptionError()).toHaveTextContent(
			"Describe briefly what volunteers can expect.",
		);
	});
});

describe("create-opportunity wizard: the shared required-field marker (#1797)", () => {
	it("marks the title field with the one aria-hidden asterisk, once explained per form", async () => {
		openWizard();

		const field = document.querySelector("#opportunity-title");
		expect(field).toHaveAccessibleName("Title");

		const label = document.querySelector("label[for='opportunity-title']");
		expect(label).toHaveTextContent(/^Title\*$/);
		const marks = label?.querySelectorAll("span[aria-hidden='true']") ?? [];
		expect(marks).toHaveLength(1);
		expect(marks[0]).toHaveTextContent("*");

		const legends = Array.from(document.querySelectorAll("p")).filter(
			(p) => p.textContent?.trim() === "* Required field",
		);
		expect(legends).toHaveLength(1);
	});
});

describe("create-opportunity wizard: distinct control names (#1957)", () => {
	it("names the header close button differently from the footer cancel button", () => {
		openWizard();

		const dialog = screen.getByRole("dialog");
		expect(within(dialog).getByTestId("modal-cancel")).toHaveTextContent(
			"Cancel",
		);
		expect(
			within(dialog).getByRole("button", { name: "Close" }),
		).toBeInTheDocument();
		expect(
			within(dialog).getAllByRole("button", { name: "Cancel" }),
		).toHaveLength(1);
	});
});

describe("create-opportunity wizard: shape and the draft gate", () => {
	it("offers all four steps and a draft button, with no accent band", async () => {
		openWizard();

		for (const n of [1, 2, 3, 4]) {
			expect(await screen.findByTestId(`wizard-stepper-${n}`)).toBeVisible();
		}
		expect(screen.getByTestId("modal-save-draft")).toBeInTheDocument();
		expect(document.querySelector(".from-brand-600")).toBeNull();
	});

	it("lets a step be reached directly, once the step before it validates", async () => {
		openWizard();

		await userEvent.click(await screen.findByTestId("wizard-stepper-2"));

		await waitFor(() => expect(titleError()).not.toBeNull());
		expect(screen.getByTestId("wizard-stepper-1")).toHaveAttribute(
			"aria-current",
			"step",
		);

		await userEvent.type(title(), "Deutscher Titel");
		await userEvent.type(description(), "Eine ausreichend lange Beschreibung.");
		await userEvent.click(screen.getByTestId("wizard-stepper-2"));

		await waitFor(() =>
			expect(screen.getByTestId("wizard-stepper-2")).toHaveAttribute(
				"aria-current",
				"step",
			),
		);
	});

	it("keeps Save draft disabled until a real title is typed", async () => {
		openWizard();

		const saveDraft = await screen.findByTestId("modal-save-draft");
		expect(saveDraft).toBeDisabled();

		await userEvent.type(title(), "   ");
		expect(saveDraft).toBeDisabled();

		await userEvent.clear(title());
		await userEvent.type(title(), "Deutscher Titel");
		await waitFor(() => expect(saveDraft).toBeEnabled());
	});
});

async function fillBasicsAndFormat(pinCode: string) {
	await userEvent.type(title(), "PIN Test Opportunity");
	await userEvent.type(
		description(),
		"Regression test for #549 organizer-settable check-in PIN.",
	);
	await userEvent.click(screen.getByTestId("wizard-stepper-2"));
	await userEvent.click(
		await screen.findByLabelText(/remote|Remote/i, { selector: "input" }),
	);
	await userEvent.click(screen.getByTestId("wizard-stepper-3"));
	await userEvent.click(
		await screen.findByRole("radio", { name: "Express interest" }),
	);
	await userEvent.click(screen.getByRole("radio", { name: "PIN code" }));
	const pinInput = screen.getByLabelText(/Check-in PIN/i);
	await userEvent.clear(pinInput);
	await userEvent.type(pinInput, pinCode);
	return pinInput;
}

describe("create-opportunity wizard: organizer-set check-in PIN (#549)", () => {
	it("sends the organizer-typed PIN exactly, in the create payload", async () => {
		openWizard();
		await fillBasicsAndFormat("482170");

		await userEvent.click(screen.getByTestId("wizard-stepper-4"));
		fireEvent.change(await screen.findByLabelText(/Interest deadline/i), {
			target: { value: "2027-06-30" },
		});

		api.createVolunteerOpportunity.mockResolvedValue({ id: "new-opp-id" });
		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(api.createVolunteerOpportunity).toHaveBeenCalledWith(
				expect.objectContaining({ checkInPin: "482170" }),
			),
		);
	});

	it("prefills the existing PIN on edit, and Generate random replaces it with a different one", async () => {
		api.getOpportunityCheckInPin.mockResolvedValue("135790");
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
						participationType: "IndividualContact",
						checkInMethod: "PINCode",
						category: undefined,
						tags: [],
						validUntil: new Date(Date.UTC(2027, 5, 30)),
					} as unknown as Parameters<
						typeof CreateVolunteerOpportunityModal
					>[0]["initialOpportunity"]
				}
				onClose={() => {}}
				onSuccess={() => {}}
			/>,
			{ auth: { isAuthenticated: true } },
		);

		await userEvent.click(await screen.findByTestId("wizard-stepper-3"));
		const pinInput = await screen.findByLabelText(/Check-in PIN/i);
		await waitFor(() => expect(pinInput).toHaveValue("135790"));

		await userEvent.click(
			screen.getByRole("button", { name: /Generate random/i }),
		);
		const generatedPin = (pinInput as HTMLInputElement).value;
		expect(generatedPin).toMatch(/^\d{6}$/);
		expect(generatedPin).not.toBe("135790");

		await userEvent.click(screen.getByTestId("wizard-stepper-4"));
		api.updateVolunteerOpportunity.mockResolvedValue(undefined);
		await userEvent.click(screen.getByTestId("modal-submit"));

		await waitFor(() =>
			expect(api.updateVolunteerOpportunity).toHaveBeenCalledWith(
				"existing-opp-id",
				expect.objectContaining({ checkInPin: generatedPin }),
			),
		);
	});
});
