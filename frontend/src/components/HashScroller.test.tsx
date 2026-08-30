import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { useEffect, useState } from "react";
import HashScroller from "./HashScroller";

// Stands in for a lazy-loaded page: the fragment target only exists once the
// route's chunk has mounted, a tick after the navigation.
function LateSection({ id }: { id: string }) {
	const [mounted, setMounted] = useState(false);
	useEffect(() => {
		const timer = setTimeout(() => setMounted(true), 0);
		return () => clearTimeout(timer);
	}, []);
	return mounted ? <section id={id}>For organizations</section> : null;
}

const scrollIntoView = vi.mocked(window.HTMLElement.prototype.scrollIntoView);

beforeEach(() => {
	scrollIntoView.mockClear();
});

describe("HashScroller", () => {
	it("scrolls to a fragment target that only appears once its chunk mounts", async () => {
		render(
			<MemoryRouter initialEntries={["/#for-organizations"]}>
				<HashScroller />
				<LateSection id="for-organizations" />
			</MemoryRouter>,
		);

		await waitFor(() => expect(scrollIntoView).toHaveBeenCalled());
	});

	it("moves focus to the target so keyboard users carry on from there", async () => {
		render(
			<MemoryRouter initialEntries={["/#for-organizations"]}>
				<HashScroller />
				<section id="for-organizations">For organizations</section>
			</MemoryRouter>,
		);

		await waitFor(() =>
			expect(document.activeElement?.id).toBe("for-organizations"),
		);
		expect(document.activeElement).toHaveAttribute("tabindex", "-1");
	});

	it("does nothing without a fragment", () => {
		render(
			<MemoryRouter initialEntries={["/"]}>
				<HashScroller />
				<section id="for-organizations">For organizations</section>
			</MemoryRouter>,
		);

		expect(scrollIntoView).not.toHaveBeenCalled();
	});
});
