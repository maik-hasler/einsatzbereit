import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DeleteSeriesSlotDialog from "./DeleteSeriesSlotDialog";
import { renderWithProviders } from "../../test/render";

function renderDialog(
	props: Partial<{
		bookedCount: number;
		loading: boolean;
		error: string | null;
	}> = {},
) {
	const onConfirm = vi.fn();
	const onClose = vi.fn();
	renderWithProviders(
		<DeleteSeriesSlotDialog
			bookedCount={props.bookedCount ?? 0}
			loading={props.loading ?? false}
			error={props.error ?? null}
			onConfirm={onConfirm}
			onClose={onClose}
		/>,
	);
	return { onConfirm, onClose };
}

describe("DeleteSeriesSlotDialog scope", () => {
	// The narrowest scope, so a mis-click removes one occurrence rather than a
	// whole series of them.
	it("starts on the single occurrence", () => {
		renderDialog();

		expect(
			screen.getByRole("radio", { name: "Only this occurrence" }),
		).toBeChecked();
	});

	it("confirms with the scope the organizer picked", async () => {
		const { onConfirm } = renderDialog();

		await userEvent.click(
			screen.getByRole("radio", { name: "This and following occurrences" }),
		);
		await userEvent.click(screen.getByRole("button", { name: "Remove" }));

		expect(onConfirm).toHaveBeenCalledExactlyOnceWith("ThisAndFollowing");
	});

	it("confirms with the default scope when nothing is picked", async () => {
		const { onConfirm } = renderDialog();

		await userEvent.click(screen.getByRole("button", { name: "Remove" }));

		expect(onConfirm).toHaveBeenCalledExactlyOnceWith("Only");
	});

	it("offers the three scopes as one group", () => {
		renderDialog();

		expect(screen.getAllByRole("radio")).toHaveLength(3);
		expect(screen.getByRole("group", { name: "Remove" })).toBeInTheDocument();
	});
});

describe("DeleteSeriesSlotDialog warning", () => {
	// The warning is about other people's sign-ups, so it is shown exactly when
	// removing more than the one occurrence would cancel some.
	it("warns when a wider scope would cancel sign-ups", async () => {
		renderDialog({ bookedCount: 3 });

		expect(screen.queryByText(/sign-up cancelled/)).not.toBeInTheDocument();

		await userEvent.click(screen.getByRole("radio", { name: "Entire series" }));

		expect(screen.getByText(/sign-up cancelled/)).toBeInTheDocument();
	});

	it("stays quiet when nobody is signed up", async () => {
		renderDialog({ bookedCount: 0 });

		await userEvent.click(screen.getByRole("radio", { name: "Entire series" }));

		expect(screen.queryByText(/sign-up cancelled/)).not.toBeInTheDocument();
	});

	it("stays quiet for the single occurrence even with sign-ups", () => {
		renderDialog({ bookedCount: 3 });

		expect(screen.queryByText(/sign-up cancelled/)).not.toBeInTheDocument();
	});
});

describe("DeleteSeriesSlotDialog state", () => {
	it("shows a failure without closing the dialog", () => {
		renderDialog({ error: "That time slot has already been removed." });

		expect(screen.getByRole("alert")).toHaveTextContent(
			"That time slot has already been removed.",
		);
		expect(screen.getByRole("dialog")).toBeInTheDocument();
	});

	// Both buttons, not just the destructive one: a second click on either while
	// the first request is open is a second request.
	it("disables both actions while the removal is in flight", () => {
		renderDialog({ loading: true });

		expect(screen.getByRole("button", { name: "Keep" })).toBeDisabled();
		expect(screen.getAllByRole("button").at(-1)).toBeDisabled();
	});

	it("closes from the keep action", async () => {
		const { onClose, onConfirm } = renderDialog();

		await userEvent.click(screen.getByRole("button", { name: "Keep" }));

		expect(onClose).toHaveBeenCalledTimes(1);
		expect(onConfirm).not.toHaveBeenCalled();
	});

	it("closes on Escape", async () => {
		const { onClose } = renderDialog();

		await userEvent.keyboard("{Escape}");

		expect(onClose).toHaveBeenCalledTimes(1);
	});
});
