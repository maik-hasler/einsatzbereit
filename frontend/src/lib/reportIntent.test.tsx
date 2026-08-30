import { describe, it, expect } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { reportIntentSigninArgs, usePendingReportIntent } from "./reportIntent";

const TARGET_ID = "33333333-3333-3333-3333-333333333333";

describe("reportIntentSigninArgs", () => {
	it("carries the target id back to the page the click happened on", () => {
		expect(
			reportIntentSigninArgs(`/organizations/${TARGET_ID}`, "", TARGET_ID),
		).toMatchObject({
			state: {
				returnTo: `/organizations/${TARGET_ID}?report=${TARGET_ID}`,
			},
		});
	});

	it("keeps the query string the visitor was already on", () => {
		expect(
			reportIntentSigninArgs("/organizations", "?q=leipzig", TARGET_ID),
		).toMatchObject({
			state: { returnTo: `/organizations?q=leipzig&report=${TARGET_ID}` },
		});
	});

	it("passes the UI locale through, like every other sign-in redirect", () => {
		expect(
			reportIntentSigninArgs("/organizations", "", TARGET_ID),
		).toHaveProperty("ui_locales");
	});
});

function Probe() {
	const pendingTargetId = usePendingReportIntent();
	const location = useLocation();
	return (
		<>
			<span data-testid="pending">{pendingTargetId ?? "none"}</span>
			<span data-testid="search">{location.search}</span>
		</>
	);
}

function renderProbe(route: string) {
	return render(
		<MemoryRouter initialEntries={[route]}>
			<Routes>
				<Route path="/organizations" element={<Probe />} />
			</Routes>
		</MemoryRouter>,
	);
}

describe("usePendingReportIntent", () => {
	it("reports the target id the visitor clicked before signing in", async () => {
		renderProbe(`/organizations?report=${TARGET_ID}`);

		expect(screen.getByTestId("pending")).toHaveTextContent(TARGET_ID);
	});

	it("strips the marker so a reload cannot reopen the modal a second time", async () => {
		renderProbe(`/organizations?q=leipzig&report=${TARGET_ID}`);

		// The intent is consumed once and stays reported for this mount; only the URL changes.
		expect(await screen.findByTestId("search")).toHaveTextContent("?q=leipzig");
		expect(screen.getByTestId("search")).not.toHaveTextContent("report=");
		expect(screen.getByTestId("pending")).toHaveTextContent(TARGET_ID);
	});

	it("reports nothing for an ordinary visit", () => {
		renderProbe("/organizations");

		expect(screen.getByTestId("pending")).toHaveTextContent("none");
	});
});

describe("report intent round trip", () => {
	it("returns the same target id the sign-in args carried out", async () => {
		const { state } = reportIntentSigninArgs(
			"/organizations",
			"?q=leipzig",
			TARGET_ID,
		);

		renderProbe(state?.returnTo ?? "/organizations");

		expect(screen.getByTestId("pending")).toHaveTextContent(TARGET_ID);
	});
});

// Guards the one thing a bare boolean flag would not: a marker naming a different entity must
// not open the modal against the wrong target (#2326).
describe("usePendingReportIntent target matching", () => {
	it("reports the id verbatim, for the caller to match against its own", () => {
		renderProbe("/organizations?report=00000000-0000-0000-0000-000000000009");

		expect(screen.getByTestId("pending")).toHaveTextContent(
			"00000000-0000-0000-0000-000000000009",
		);
	});
});
