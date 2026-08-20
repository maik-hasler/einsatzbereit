import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import DangerZoneCard from "./DangerZoneCard";
import { renderWithProviders } from "../../test/render";

vi.mock("../../hooks/useApiClient", () => ({
	useApiClient: () => ({ deleteMyAccount: vi.fn() }),
}));

/** The /profile/settings half of `DangerZonePanelTests` (#1792). */
describe("ProfileSettingsPage danger zone", () => {
	it("is headed by the action it performs, not a generic 'Danger zone'", () => {
		renderWithProviders(<DangerZoneCard />, {
			auth: { isAuthenticated: true },
		});

		expect(
			screen.getByRole("heading", { name: "Delete account" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("heading", { name: /Danger zone/i })).toBeNull();
		expect(
			screen.getByRole("button", { name: "Delete my account" }),
		).toBeInTheDocument();
	});
});
