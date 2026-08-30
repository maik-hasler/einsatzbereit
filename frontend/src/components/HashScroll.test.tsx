import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { fireEvent, screen, waitFor } from "@testing-library/react";
import { useState } from "react";
import HashScroll from "./HashScroll";
import { renderWithProviders } from "../test/render";

// jsdom has no layout engine, so scrollIntoView does nothing there - what these
// assert is which element we ask to scroll to, which is the part that was
// missing entirely (#2331). src/test/setup.ts already stubs it on
// HTMLElement.prototype, so spy on that same prototype rather than on Element's.
let scrolled: string[];

beforeEach(() => {
	scrolled = [];
	vi.spyOn(HTMLElement.prototype, "scrollIntoView").mockImplementation(
		function (this: HTMLElement) {
			scrolled.push(this.id);
		},
	);
});

afterEach(() => {
	vi.restoreAllMocks();
});

function Sections() {
	return (
		<>
			<section id="scope">Scope</section>
			<section id="liability">Liability</section>
		</>
	);
}

// Stands in for a lazily-loaded route: at the moment the document loads at a
// fragment URL the section does not exist yet, which is exactly why the
// browser's own fragment scrolling does nothing.
function LateSections() {
	const [ready, setReady] = useState(false);
	return (
		<>
			<button onClick={() => setReady(true)}>reveal</button>
			{ready && <Sections />}
		</>
	);
}

describe("HashScroll", () => {
	it("scrolls to the section named by the fragment on a full document load", async () => {
		renderWithProviders(
			<>
				<HashScroll />
				<Sections />
			</>,
			{ route: "/terms-of-use#liability" },
		);

		await waitFor(() => expect(scrolled).toEqual(["liability"]));
	});

	it("waits for a lazily-mounted section instead of giving up", async () => {
		renderWithProviders(
			<>
				<HashScroll />
				<LateSections />
			</>,
			{ route: "/terms-of-use#liability" },
		);

		expect(scrolled).toEqual([]);

		fireEvent.click(screen.getByText("reveal"));

		await waitFor(() => expect(scrolled).toEqual(["liability"]));
	});

	it("moves focus to the section so keyboard users continue from there", async () => {
		renderWithProviders(
			<>
				<HashScroll />
				<Sections />
			</>,
			{ route: "/terms-of-use#liability" },
		);

		await waitFor(() =>
			expect(document.activeElement).toBe(document.getElementById("liability")),
		);
	});

	it("does nothing without a fragment", async () => {
		renderWithProviders(
			<>
				<HashScroll />
				<Sections />
			</>,
			{ route: "/terms-of-use" },
		);

		await waitFor(() => expect(scrolled).toEqual([]));
	});

	it("ignores a fragment that names no section", async () => {
		renderWithProviders(
			<>
				<HashScroll />
				<Sections />
			</>,
			{ route: "/terms-of-use#does-not-exist" },
		);

		await waitFor(() => expect(scrolled).toEqual([]));
	});
});
