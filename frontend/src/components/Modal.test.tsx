import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRef, useState as useReactState } from "react";
import Modal from "./Modal";
import { renderWithProviders } from "../test/render";

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
		renderWithProviders(<Harness />);

		const trigger = screen.getByRole("button", { name: "Create opportunity" });
		await userEvent.click(trigger);

		expect(screen.getByRole("textbox", { name: "Title" })).toHaveFocus();

		await userEvent.keyboard("{Escape}");

		expect(screen.queryByRole("dialog")).toBeNull();
		expect(trigger).toHaveFocus();
	});
});
