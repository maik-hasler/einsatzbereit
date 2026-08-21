import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import Modal from "./Modal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("Modal a11y", () => {
	function renderModal(children = <p>Body copy.</p>) {
		return renderWithProviders(
			<div>
				<h2 id="modal-title">Sign up</h2>
				<Modal labelledBy="modal-title" onClose={() => {}}>
					{children}
				</Modal>
			</div>,
		);
	}

	it("has no violations, backdrop included", async () => {
		renderModal();
		await expectNoA11yViolations();
	});

	it("exposes a modal dialog named by the heading it points at", async () => {
		renderModal();
		const dialog = screen.getByRole("dialog");
		expect(dialog).toHaveAttribute("aria-modal", "true");
		expect(dialog).toHaveAccessibleName("Sign up");
	});

	it("keeps the backdrop out of the tab order while hiding it from the a11y tree", async () => {
		renderModal();
		const backdrop = document.querySelector('button[aria-hidden="true"]');
		expect(backdrop).not.toBeNull();
		expect(backdrop).toHaveAttribute("tabindex", "-1");
		await expectNoA11yViolations();
	});

	it("has no violations with a nested dialog suspending the outer one", async () => {
		renderWithProviders(
			<div>
				<h2 id="outer-title">Organization settings</h2>
				<Modal labelledBy="outer-title" onClose={() => {}} suspended>
					<p>Body copy.</p>
					<h2 id="inner-title">Discard changes?</h2>
					<Modal labelledBy="inner-title" onClose={() => {}}>
						<button type="button">Discard</button>
					</Modal>
				</Modal>
			</div>,
		);
		expect(screen.getAllByRole("dialog")).toHaveLength(2);
		await expectNoA11yViolations();
	});
});
