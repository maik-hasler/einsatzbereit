import { describe, it, expect } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import { dispatchToast } from "../lib/toastBus";
import { renderWithProviders } from "../test/render";

/**
 * Was `ToastDeduplicationTests` (#760), moved down in #2148 wave 2.
 *
 * The Playwright original drove a real login, then intercepted every
 * authenticated GET with a 403 (complete with a hand-written
 * Access-Control-Allow-Origin header, because the browser enforces CORS on
 * fulfilled responses too) and reloaded the page to make the header and
 * landing page re-fire their concurrent requests - all to get four identical
 * toasts dispatched at once. Four dispatchToast calls do the same thing.
 */
describe("ToastProvider", () => {
	it("coalesces identical simultaneous toasts into one", () => {
		// #760: the home page fires several concurrent authenticated GETs on
		// load. When they all failed with the same "forbidden" error - as they
		// did whenever a token was missing an expected role - the bus stacked
		// one identical toast per failed request.
		renderWithProviders(<div />);

		// One act() around all four, so they land in the same React batch -
		// which is the concurrency the bug needed.
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
		// #972: an accessibility audit found zero aria-live regions across 16
		// page loads, because the toast container only entered the DOM once a
		// toast already existed - a screen reader has nothing to pick up until
		// the content is already stale.
		renderWithProviders(<div />);

		const sentinel = document.querySelector(
			"[data-testid='toast-live-region'][aria-live='polite']",
		);
		expect(sentinel).not.toBeNull();
		expect(sentinel).toBeEmptyDOMElement();
	});

	it("keeps each toast's role=alert a sibling of the sentinel, never nested inside it", () => {
		// Nesting live regions is unreliable across screen readers, so the
		// sentinel sits alongside the toast list rather than wrapping it.
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
