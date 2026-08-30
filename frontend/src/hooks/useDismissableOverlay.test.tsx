import { describe, it, expect } from "vitest";
import { useState } from "react";
import { act, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useDismissableOverlay } from "./useDismissableOverlay";

function Disclosure() {
	const [open, setOpen] = useState(false);
	const ref = useDismissableOverlay<HTMLDivElement>(open, () => setOpen(false));

	return (
		<div>
			<div ref={ref}>
				<button
					type="button"
					aria-expanded={open}
					onClick={() => setOpen((o) => !o)}
				>
					Toggle
				</button>
				{open && (
					<div>
						<button type="button">Inside</button>
					</div>
				)}
			</div>
			<button type="button">Outside</button>
		</div>
	);
}

async function openPanel() {
	render(<Disclosure />);
	const trigger = screen.getByRole("button", { name: "Toggle" });
	await userEvent.click(trigger);
	expect(trigger).toHaveAttribute("aria-expanded", "true");
	return trigger;
}

describe("useDismissableOverlay focus handling", () => {
	it("dismisses the overlay once focus lands outside it", async () => {
		const trigger = await openPanel();

		act(() => screen.getByRole("button", { name: "Outside" }).focus());

		expect(trigger).toHaveAttribute("aria-expanded", "false");
	});

	it("leaves the overlay open while focus moves within it", async () => {
		const trigger = await openPanel();

		act(() => screen.getByRole("button", { name: "Inside" }).focus());

		expect(trigger).toHaveAttribute("aria-expanded", "true");
	});

	// Picking an option unmounts the focused node, which moves focus to `body`
	// without anything gaining it. Dismissing on that would tear an overlay down
	// mid-interaction, which is why the hook listens for `focusin` and not
	// `focusout`.
	it("stays open when the focused element inside it is removed", async () => {
		const trigger = await openPanel();

		const inside = screen.getByRole("button", { name: "Inside" });
		act(() => {
			inside.focus();
			inside.remove();
		});

		expect(trigger).toHaveAttribute("aria-expanded", "true");
	});
});
