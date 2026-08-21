import { describe, it, expect } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import { dispatchToast } from "../lib/toastBus";
import { renderWithProviders } from "../test/render";

describe("ToastProvider", () => {
	it("coalesces identical simultaneous toasts into one", () => {
		renderWithProviders(<div />);

		act(() => {
			for (let i = 0; i < 4; i++) {
				dispatchToast("error", "You do not have permission to do this.");
			}
		});

		const toasts = screen
			.getAllByRole("alert")
			.filter((el) => el.textContent?.includes("You do not have permission"));
		expect(toasts).toHaveLength(1);
	});

	it("still shows genuinely different messages side by side", async () => {
		renderWithProviders(<div />);

		act(() => {
			dispatchToast("error", "First problem.");
			dispatchToast("error", "Second problem.");
		});

		await waitFor(() => {
			expect(screen.getByText("First problem.")).toBeInTheDocument();
			expect(screen.getByText("Second problem.")).toBeInTheDocument();
		});
	});

	it("mounts an empty live-region sentinel before any toast has fired", () => {
		renderWithProviders(<div />);

		const sentinel = document.querySelector(
			"[data-testid='toast-live-region'][aria-live='polite']",
		);
		expect(sentinel).not.toBeNull();
		expect(sentinel).toBeEmptyDOMElement();
	});

	it("keeps each toast's role=alert a sibling of the sentinel, never nested inside it", () => {
		renderWithProviders(<div />);
		act(() => {
			dispatchToast("error", "You do not have permission to do this.");
		});

		const sentinel = document.querySelector(
			"[data-testid='toast-live-region']",
		);
		expect(sentinel).not.toBeNull();
		expect(sentinel?.querySelectorAll("[role='alert']")).toHaveLength(0);
		expect(screen.getByRole("alert")).toBeInTheDocument();
	});
});
