import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SubmitFeedbackModal from "./SubmitFeedbackModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * Replaces `MyEngagementsPage_EditableFeedback_AsVera_*` and the feedback
 * half of `EngagementManagementPage_*`. The star rating is a
 * `role="group"` of aria-pressed toggles rather than a radio group, and the
 * reason (ARIA does not allow aria-required on `group`) is a claim only a
 * scan plus a direct assertion can hold in place.
 */
const { api } = vi.hoisted(() => ({
	api: { submitFeedback: vi.fn(), updateFeedback: vi.fn() },
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	vi.clearAllMocks();
});

describe("SubmitFeedbackModal a11y", () => {
	it("has no violations when submitting feedback for the first time", async () => {
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations when editing feedback that already exists", async () => {
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				initialRating={4}
				initialComment="Well organized."
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations once the submission failed", async () => {
		api.submitFeedback.mockRejectedValue(new Error("boom"));
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);

		const stars = screen.getAllByRole("button", { pressed: false });
		await userEvent.click(stars[stars.length - 1]);
		await userEvent.click(
			screen.getByRole("button", { name: "Submit feedback" }),
		);

		await screen.findByRole("alert");
		await expectNoA11yViolations();
	});

	it("names the rating group and reflects the chosen star with aria-pressed", async () => {
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				initialRating={3}
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);
		const group = screen.getByRole("group");
		expect(group).toHaveAccessibleName();
		expect(group).not.toHaveAttribute("aria-required");
		expect(screen.getAllByRole("button", { pressed: true })).toHaveLength(1);
	});
});
