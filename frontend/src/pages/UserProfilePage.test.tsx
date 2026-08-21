import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import UserProfilePage from "./UserProfilePage";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const USER_ID = "11111111-1111-1111-1111-111111111111";

beforeEach(() => {
	api.__reset();
	api.getPublicUserProfile.mockResolvedValue({
		displayName: "Vera Volunteer",
		engagementCount: 3,
		badges: [],
		avatarUrl: undefined,
		bio: undefined,
		skills: [],
		languages: [],
	});
	api.getBadgeCatalog.mockResolvedValue([]);
});

describe("UserProfilePage badges", () => {
	it("shows them inline, with no link to the retired achievements page", async () => {
		renderWithProviders(
			<Routes>
				<Route path="/users/:userId" element={<UserProfilePage />} />
			</Routes>,
			{ route: `/users/${USER_ID}`, auth: { isAuthenticated: true } },
		);

		expect(
			await screen.findByRole("heading", { name: "Badges" }),
		).toBeVisible();
		expect(screen.queryByRole("link", { name: /achievements/i })).toBeNull();
		expect(
			document.querySelector(`a[href="/users/${USER_ID}/achievements"]`),
		).toBeNull();
	});
});
