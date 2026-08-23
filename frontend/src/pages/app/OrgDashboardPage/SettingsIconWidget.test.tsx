import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import SettingsIconWidget from "./SettingsIconWidget";
import { renderWithProviders } from "../../../test/render";

const ORG_ID = "11111111-1111-1111-1111-111111111111";

describe("SettingsIconWidget compact placement", () => {
	it("shows the Settings label alongside the icon, not the icon alone", () => {
		renderWithProviders(
			<SettingsIconWidget organizationId={ORG_ID} size="compact" />,
		);

		expect(screen.getAllByText("Settings").length).toBeGreaterThanOrEqual(2);
		expect(
			document.querySelector("span.text-sm.font-medium"),
		).toHaveTextContent("Settings");
		expect(screen.getByRole("link", { name: "Settings" })).toHaveAttribute(
			"href",
			`/app/${ORG_ID}/dashboard/settings`,
		);
	});
});
