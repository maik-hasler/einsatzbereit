import { describe, it, expect, beforeEach, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { Link, Route, Routes } from "react-router";
import RouteAnnouncer from "./RouteAnnouncer";
import { renderWithProviders } from "../test/render";

function FirstPage() {
	return (
		<div>
			<h1>First page</h1>
			<Link to="/second">Go to second</Link>
		</div>
	);
}

function SecondPage() {
	return <h1>Second page</h1>;
}

// Stands in for a lazy-loaded route whose chunk (and h1) isn't there yet
// when the location changes - the reveal is a click instead of a real delay
// so the test doesn't depend on timing.
function SlowSecondPage() {
	const [ready, setReady] = useState(false);
	return ready ? (
		<h1>Second page</h1>
	) : (
		<button onClick={() => setReady(true)}>reveal</button>
	);
}

function renderApp(route: string, slow = false) {
	return renderWithProviders(
		<>
			<RouteAnnouncer />
			<main id="main-content" tabIndex={-1}>
				<Routes>
					<Route path="/first" element={<FirstPage />} />
					<Route
						path="/second"
						element={slow ? <SlowSecondPage /> : <SecondPage />}
					/>
				</Routes>
			</main>
		</>,
		{ route },
	);
}

function liveRegion() {
	const region = document.querySelector('[aria-live="polite"]');
	if (!region) throw new Error("live region not found");
	return region;
}

beforeEach(() => {
	vi.mocked(window.scrollTo).mockClear();
});

describe("RouteAnnouncer", () => {
	it("leaves focus and the live region alone on the initial page load", () => {
		renderApp("/first");

		expect(document.activeElement).toBe(document.body);
		expect(liveRegion()).toHaveTextContent("");
		expect(window.scrollTo).not.toHaveBeenCalled();
	});

	it("moves focus to the new page's h1, resets scroll and announces it after a client-side navigation", async () => {
		const user = userEvent.setup();
		renderApp("/first");

		await user.click(screen.getByRole("link", { name: "Go to second" }));

		const heading = await screen.findByRole("heading", { name: "Second page" });
		expect(document.activeElement).toBe(heading);
		expect(heading).toHaveAttribute("tabindex", "-1");
		expect(window.scrollTo).toHaveBeenCalledWith(0, 0);
		expect(liveRegion()).toHaveTextContent("Second page");
	});

	it("falls back to #main-content and still catches the heading once it mounts later", async () => {
		const user = userEvent.setup();
		renderApp("/first", true);

		await user.click(screen.getByRole("link", { name: "Go to second" }));

		expect(window.scrollTo).toHaveBeenCalledWith(0, 0);
		expect(
			screen.queryByRole("heading", { name: "Second page" }),
		).not.toBeInTheDocument();
		expect(document.activeElement).toBe(
			document.getElementById("main-content"),
		);

		await user.click(screen.getByRole("button", { name: "reveal" }));

		const heading = await screen.findByRole("heading", { name: "Second page" });
		await waitFor(() => expect(document.activeElement).toBe(heading));
		await waitFor(() => expect(liveRegion()).toHaveTextContent("Second page"));
	});
});
