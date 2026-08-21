import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ConfirmDialog from "./ConfirmDialog";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("ConfirmDialog a11y", () => {
	it("has no violations in its resting state", async () => {
		renderWithProviders(
			<ConfirmDialog
				title="Cancel this engagement?"
				message="The volunteer will be notified."
				confirmLabel="Cancel engagement"
				onConfirm={() => {}}
				onClose={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations while the confirmed action is in flight", async () => {
		renderWithProviders(
			<ConfirmDialog
				title="Cancel this engagement?"
				message="The volunteer will be notified."
				confirmLabel="Cancel engagement"
				loading
				onConfirm={() => {}}
				onClose={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations when the action failed and an error banner is shown", async () => {
		renderWithProviders(
			<ConfirmDialog
				title="Cancel this engagement?"
				message="The volunteer will be notified."
				confirmLabel="Cancel engagement"
				error="The engagement could not be cancelled."
				onConfirm={() => {}}
				onClose={() => {}}
			/>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations with an extra detail field between message and actions", async () => {
		renderWithProviders(
			<ConfirmDialog
				title="Cancel this engagement?"
				message="The volunteer will be notified."
				confirmLabel="Cancel engagement"
				onConfirm={() => {}}
				onClose={() => {}}
			>
				<label htmlFor="reason">Reason (optional)</label>
				<textarea id="reason" />
			</ConfirmDialog>,
		);
		await expectNoA11yViolations();
	});

	it("names the dialog by its own heading", async () => {
		renderWithProviders(
			<ConfirmDialog
				title="Cancel this engagement?"
				message="The volunteer will be notified."
				confirmLabel="Cancel engagement"
				onConfirm={() => {}}
				onClose={() => {}}
			/>,
		);
		expect(screen.getByRole("dialog")).toHaveAccessibleName(
			"Cancel this engagement?",
		);
	});
});
