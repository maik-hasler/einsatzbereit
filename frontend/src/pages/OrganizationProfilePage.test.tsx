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

	// Organizations carry no English description to fall back from, so unlike
	// an opportunity this text is always the German original - and used to say
	// nothing about that, while a German-only opportunity right beside it did
	// (#2328).
	it("discloses that the description is German-only on an English page", async () => {
		renderProfile();

		expect(
			await screen.findByText("Only available in German"),
		).toBeInTheDocument();
	});

	it("leaves the notice off the German page, where it says nothing", async () => {
		renderWithProviders(
			<Routes>
				<Route
					path="/organizations/:organizationId"
					element={<OrganizationProfilePage />}
				/>
			</Routes>,
			{ lng: "de", route: `/organizations/${ORGANIZATION_ID}` },
		);

		await screen.findByText(
			"Wir unterstuetzen Menschen in Leipzig und Umgebung.",
		);
		expect(
			screen.queryByText("Nur auf Deutsch verfügbar"),
		).not.toBeInTheDocument();
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

describe("OrganizationProfilePage organization logo", () => {
	it("renders the uploaded logo the profile response carries", async () => {
		api.getPublicOrganizationProfile.mockResolvedValue({
			id: ORGANIZATION_ID,
			name: "Nachbarschaftshilfe Leipzig",
			logoUrl: "https://storage.example.test/org-logos/leipzig.webp",
			openOpportunities: [],
		});
		const { container } = renderProfile();

		await screen.findByRole("heading", { name: "Nachbarschaftshilfe Leipzig" });

		// A decorative `alt=""` image has no `img` role, so query it by tag.
		const logo = container.ownerDocument.querySelector(
			'img[src="https://storage.example.test/org-logos/leipzig.webp"]',
		);
		expect(logo).not.toBeNull();
	});

	it("falls back to the organization's initials when it has no logo", async () => {
		renderProfile();

		await screen.findByRole("heading", { name: "Nachbarschaftshilfe Leipzig" });

		expect(await screen.findByText("NL")).toBeInTheDocument();
	});
});

describe("OrganizationProfilePage missing organization", () => {
	it("shows the not-found state with a way back instead of a retry that cannot succeed", async () => {
		api.getPublicOrganizationProfile.mockRejectedValue({ status: 404 });
		renderProfile();

		const failure = await screen.findByTestId("organization-load-failure");

		expect(
			screen.getByRole("heading", { level: 1, name: "Organization not found" }),
		).toBeInTheDocument();
		expect(
			screen.queryByRole("button", { name: "Try again" }),
		).not.toBeInTheDocument();
		expect(
			await screen.findByRole("link", { name: "Organizations" }),
		).toHaveAttribute("href", "/organizations");
		expect(failure).toBeInTheDocument();
	});

	it("does not leave the tab title on the loading placeholder", async () => {
		api.getPublicOrganizationProfile.mockRejectedValue({ status: 404 });
		renderProfile();

		await screen.findByTestId("organization-load-failure");

		expect(document.title).toBe("Organization not found | Einsatzbereit");
	});

	it("keeps a retry for a genuine server error", async () => {
		api.getPublicOrganizationProfile.mockRejectedValue({ status: 500 });
		renderProfile();

		await screen.findByTestId("organization-load-failure");

		expect(
			screen.getByRole("heading", { level: 1, name: "Something went wrong" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});
});
