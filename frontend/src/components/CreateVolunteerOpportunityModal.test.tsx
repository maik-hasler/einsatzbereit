import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CreateVolunteerOpportunityModal from "./CreateVolunteerOpportunityModal";
import { renderWithProviders } from "../test/render";

/**
 * Was `WizardBlockedStepJumpTests` (#1782),
 * `WizardFocusFirstInvalidFieldTests` (#2077) and
 * `WizardLiveRevalidationTests` (#1928) in the Playwright suite, moved down
 * in #2148 wave 2.
 *
 * All three are about react-hook-form behaviour inside this one component:
 * this wizard never calls `handleSubmit()` - "Next", a stepper jump and the
 * final submit all call `trigger()` directly - so neither
 * `shouldFocusError`'s focus management nor `reValidateMode`'s per-keystroke
 * revalidation comes for free, and both had to be wired by hand. Nothing in
 * any of them needs a real backend or real layout; each one used to pay a
 * login and a dashboard navigation to reach a dialog that renders from props.
 */
const { api } = vi.hoisted(() => ({
	api: {
		getOrganizationDetails: vi.fn(),
		createVolunteerOpportunity: vi.fn(),
		updateVolunteerOpportunity: vi.fn(),
		createTimeSlot: vi.fn(),
		publishVolunteerOpportunity: vi.fn(),
		uploadOpportunityBanner: vi.fn(),
		getVolunteerOpportunityDetails: vi.fn(),
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
			expect(titleError()).toHaveTextContent("Please fill this in."),
		);
		expect(title()).toHaveFocus();
	});

	it("focuses the description when only it is blank, not the title", async () => {
		openWizard();
		await userEvent.type(title(), "Focus regression test");
		await userEvent.click(screen.getByTestId("modal-next"));

		await waitFor(() =>
			expect(descriptionError()).toHaveTextContent("Please fill this in."),
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

		// Reachable from the control that was refused, not just announced once
		// into the void. The button stays enabled on purpose - a disabled one
		// takes itself out of the tab order and could not carry the
		// explanation either.
		expect(screen.getByTestId("wizard-stepper-4")).toHaveAttribute(
			"aria-describedby",
			"create-opportunity-step-blocked",
		);

		// And the jump really did not happen.
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
		// The case the silent bail hurt most: the field standing in the way is
		// on a step the user is not looking at, so the red rule on that step's
		// marker was the only clue, with nothing saying it stopped the jump.
		openWizard();
		await userEvent.type(title(), "Intermediate step block");
		await userEvent.type(description(), "Regression test for #1782.");

		// Step 2's address arrives pre-filled from the organization, so break
		// it deliberately rather than depending on which fields come up empty.
		await userEvent.click(screen.getByTestId("wizard-stepper-2"));
		await waitFor(() =>
			expect(screen.getByTestId("wizard-step-2")).toBeInTheDocument(),
		);
		const city = document.querySelector("#opportunity-city") as HTMLElement;
		await userEvent.clear(city);

		// Back to step 1 (a backwards jump is never validated), then forward
		// past the broken step 2.
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
		// react-hook-form only re-validates an already-errored field on every
		// keystroke once the form has been through handleSubmit() - its
		// reValidateMode default only takes effect after isSubmitted flips,
		// and this wizard never calls handleSubmit at all.
		openWizard();
		await userEvent.click(screen.getByTestId("modal-next"));

		await waitFor(() =>
			expect(titleError()).toHaveTextContent("Please fill this in."),
		);
		expect(descriptionError()).toHaveTextContent("Please fill this in.");
		expect(title()).toHaveAttribute("aria-invalid", "true");

		await userEvent.type(title(), "Erste-Hilfe-Kurs fuer Anfaenger");

		await waitFor(() => expect(titleError()).toBeNull());
		expect(title()).not.toHaveAttribute("aria-invalid");

		// Per-field, not a blanket clear of every error on the step.
		expect(descriptionError()).toHaveTextContent("Please fill this in.");
	});
});

describe("create-opportunity wizard: the shared required-field marker (#1797)", () => {
	it("marks the title field with the one aria-hidden asterisk, once explained per form", async () => {
		// Required fields used to be marked three different ways across the
		// product - this component's own RequiredMark, an asterisk baked into
		// the translated string ("Name *"), and a spelled-out "(required)".
		// The baked-in variant could not be aria-hidden, so its field
		// announced as "Name star". OrgSettingsPage.test.tsx asserts the same
		// convention on the other form the fix touched.
		openWizard();

		// Both content languages render a Title field; the inactive one is
		// hidden by a Tailwind `hidden` class, which jsdom has no stylesheet
		// to apply - so target the German field by id rather than by role and
		// name. (`screen.getByRole` would find two.)
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
		// Both used to read their accessible name from the same
		// createOpportunity.cancel key, so two distinct controls in one dialog
		// were indistinguishable by name (WCAG 2.2 SC 4.1.2).
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
