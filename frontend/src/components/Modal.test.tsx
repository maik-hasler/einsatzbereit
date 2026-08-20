import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRef, useState as useReactState } from "react";
import Modal from "./Modal";
import { renderWithProviders } from "../test/render";

/**
 * Was `ModalFocusRestoreTests` (#1670), moved down in #2148 wave 2. The
 * Playwright original signed in as olaf, navigated to the org dashboard and
 * opened the create-opportunity wizard purely to have *a* modal with a
 * focusable child - which is the condition the bug depended on, and which a
 * two-element fixture reproduces exactly.
 */
function Harness() {
	const [open, setOpen] = useReactState(false);
	const bodyRef = useRef<HTMLDivElement>(null);
	return (
		<div>
			<button type="button" onClick={() => setOpen(true)}>
				Create opportunity
			</button>
			{open && (
				<Modal
					labelledBy="harness-title"
					initialFocusRef={bodyRef}
					onClose={() => setOpen(false)}
				>
					<h2 id="harness-title">Create opportunity</h2>
					<button type="button">Close</button>
					<div ref={bodyRef}>
						<input aria-label="Title" />
					</div>
				</Modal>
			)}
		</div>
	);
}

describe("Modal focus management", () => {
	it("moves focus into the dialog on open and back to the trigger on close", async () => {
		// #1670: Modal's trigger-capture effect was declared *after* the effect
		// that moves focus into the dialog. React fires mount effects in
		// declaration order, so the capture read document.activeElement only
		// once the other effect had already focused something inside the
		// dialog - it captured that inner element instead of the button that
		// opened the modal, and restore-on-close silently no-oped.
		renderWithProviders(<Harness />);

		const trigger = screen.getByRole("button", { name: "Create opportunity" });
		await userEvent.click(trigger);

		// initialFocusRef scopes the initial-focus search past the header close
		// button, exactly as the wizard does - so focus lands on a focusable
		// child inside the dialog, the condition #1670 needed.
		expect(screen.getByRole("textbox", { name: "Title" })).toHaveFocus();

		await userEvent.keyboard("{Escape}");

		expect(screen.queryByRole("dialog")).toBeNull();
		expect(trigger).toHaveFocus();
	});
});
