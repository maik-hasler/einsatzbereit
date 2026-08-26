import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import SubmitFeedbackModal from "./SubmitFeedbackModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

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

		const stars = screen.getAllByRole("radio");
		await userEvent.click(stars[stars.length - 1]);
		await userEvent.click(
			screen.getByRole("button", { name: "Submit feedback" }),
		);

		await screen.findByRole("alert");
		await expectNoA11yViolations();
	});

	it("names the rating radiogroup and reflects the chosen star with aria-checked", async () => {
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				initialRating={3}
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);
		const group = screen.getByRole("radiogroup");
		expect(group).toHaveAccessibleName();
		expect(group).not.toHaveAttribute("aria-required");
		expect(screen.getAllByRole("radio", { checked: true })).toHaveLength(1);
	});

	it("moves selection with arrow keys, keeping a single tab stop", async () => {
		renderWithProviders(
			<SubmitFeedbackModal
				engagementId="eng-1"
				opportunityTitle="Beach cleanup"
				initialRating={2}
				onSubmitted={() => {}}
				onClose={() => {}}
			/>,
		);
		const stars = screen.getAllByRole("radio");
		expect(stars.filter((s) => s.tabIndex === 0)).toHaveLength(1);

		stars[1].focus();
		await userEvent.keyboard("{ArrowRight}");

		expect(stars[2]).toHaveFocus();
		expect(stars[2]).toHaveAttribute("aria-checked", "true");
		expect(stars[1]).toHaveAttribute("aria-checked", "false");
	});
});
