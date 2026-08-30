import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import ConfirmDialog from "./ConfirmDialog";
import { renderWithProviders } from "../test/render";

function renderDialog(
	props: Partial<Parameters<typeof ConfirmDialog>[0]> = {},
) {
	return renderWithProviders(
		<ConfirmDialog
			title="Restore this content?"
			message="It will become visible again."
			confirmLabel="Yes, restore"
			onConfirm={() => {}}
			onClose={() => {}}
			{...props}
		/>,
		{ lng: "en" },
	);
}

// Red is the signal for "this removes or hides something". Painting restore and promote in the
// same alarm red as a shadow-delete left the colour carrying no signal at all (#2326).
describe("ConfirmDialog tone", () => {
	it("paints a destructive confirmation red by default", () => {
		renderDialog({ confirmLabel: "Yes, hide it" });

		expect(screen.getByRole("button", { name: "Yes, hide it" })).toHaveClass(
			"bg-red-600",
		);
	});

	it("keeps a constructive confirmation on the primary brand button", () => {
		renderDialog({ tone: "constructive" });

		const confirm = screen.getByRole("button", { name: "Yes, restore" });
		expect(confirm).toHaveClass("bg-brand-700");
		expect(confirm).not.toHaveClass("bg-red-600");
	});

	it('defaults the cancel label to "Keep", which only reads right against a removal', () => {
		renderDialog({ confirmLabel: "Yes, hide it" });

		expect(screen.getByRole("button", { name: "Keep" })).toBeInTheDocument();
	});

	it("lets a constructive dialog name its own way out", () => {
		renderDialog({ tone: "constructive", cancelLabel: "Cancel" });

		expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Keep" })).toBeNull();
	});
});
