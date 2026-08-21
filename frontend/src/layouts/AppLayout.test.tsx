import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import { Route, Routes } from "react-router";
import AppLayout from "./AppLayout";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
});

function renderAt(route: string) {
	return renderWithProviders(
		<Routes>
			<Route element={<AppLayout />}>
				<Route path="/opportunities" element={<div>list</div>} />
				<Route path="/organizations/:id" element={<div>profile</div>} />
			</Route>
		</Routes>,
		{ route },
	);
}

const footerHeadingLevels = () => {
	const footer = screen.getByRole("contentinfo");
	return within(footer)
		.getAllByRole("heading")
		.map((h) => Number(h.tagName.slice(1)));
};

describe("AppLayout footer headings", () => {
	it("demotes them to level 3 on the opportunities grid", () => {
		renderAt("/opportunities");

		const levels = footerHeadingLevels();
		expect(levels.length).toBeGreaterThan(0);
		expect(new Set(levels)).toEqual(new Set([3]));
	});

	it("leaves them at level 2 everywhere else", () => {
		renderAt("/organizations/11111111-1111-1111-1111-111111111111");

		const levels = footerHeadingLevels();
		expect(levels.length).toBeGreaterThan(0);
		expect(new Set(levels)).toEqual(new Set([2]));
	});
});
