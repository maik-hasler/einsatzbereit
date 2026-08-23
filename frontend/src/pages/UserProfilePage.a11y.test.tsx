import { describe, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import UserProfilePage from "./UserProfilePage";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const USER_ID = "11111111-1111-1111-1111-111111111111";

function renderPage() {
	return renderWithProviders(
		<Routes>
			<Route path="/users/:userId" element={<UserProfilePage />} />
		</Routes>,
		{ route: `/users/${USER_ID}`, auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
	api.getBadgeCatalog.mockResolvedValue([]);
});

describe("UserProfilePage a11y", () => {
	it("has no violations for an empty profile (no bio/skills/languages)", async () => {
		api.getPublicUserProfile.mockResolvedValue({
			displayName: "Vera Volunteer",
			engagementCount: 0,
			badges: [],
			avatarUrl: undefined,
			bio: undefined,
			skills: [],
			languages: [],
		});

		renderPage();

		await screen.findByText("This profile is still empty");
		await expectNoA11yViolations();
	});

	it("has no violations for a populated profile", async () => {
		api.getPublicUserProfile.mockResolvedValue({
			displayName: "Vera Volunteer",
			engagementCount: 3,
			badges: [],
			avatarUrl: undefined,
			bio: "I love volunteering on weekends.",
			skills: ["First aid"],
			languages: ["German"],
		});

		renderPage();

		await screen.findByText("First aid");
		await expectNoA11yViolations();
	});
});
