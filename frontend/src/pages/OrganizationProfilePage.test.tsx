import { describe, it, expect, beforeEach, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes } from "react-router";
import OrganizationProfilePage from "./OrganizationProfilePage";
import { renderWithProviders, type TestAuth } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORGANIZATION_ID = "33333333-3333-3333-3333-333333333333";

beforeEach(() => {
	api.__reset();
	api.getPublicOrganizationProfile.mockResolvedValue({
		id: ORGANIZATION_ID,
		name: "Nachbarschaftshilfe Leipzig",
		description: "Wir unterstuetzen Menschen in Leipzig und Umgebung.",
		openOpportunities: [],
	});
});

function renderProfile(auth?: TestAuth) {
	return renderWithProviders(
		<Routes>
			<Route
				path="/organizations/:organizationId"
				element={<OrganizationProfilePage />}
			/>
		</Routes>,
		{ lng: "en", route: `/organizations/${ORGANIZATION_ID}`, auth },
	);
}

describe("OrganizationProfilePage description language", () => {
	it("marks the organization's German description as German on an English page", async () => {
		renderProfile();

		const description = await screen.findByText(
			"Wir unterstuetzen Menschen in Leipzig und Umgebung.",
		);
		expect(description).toHaveAttribute("lang", "de");
	});
});

describe("OrganizationProfilePage anonymous visitor", () => {
	it("returns the visitor to this organization after signing in from the report action", async () => {
		const signinRedirect = vi.fn().mockResolvedValue(undefined);
		renderProfile({ isAuthenticated: false, signinRedirect });

		await userEvent.click(await screen.findByTestId("report-organization"));

		expect(signinRedirect).toHaveBeenCalledWith(
			expect.objectContaining({
				state: { returnTo: `/organizations/${ORGANIZATION_ID}` },
			}),
		);
	});
});
