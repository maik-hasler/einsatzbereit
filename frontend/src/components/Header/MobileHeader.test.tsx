import { describe, it, expect } from "vitest";
import { createRef } from "react";
import { screen } from "@testing-library/react";
import MobileHeader from "./MobileHeader";
import { renderWithProviders } from "../../test/render";
import type { AccountMenuState } from "../../hooks/useAccountMenu";

describe("MobileHeader toggle label", () => {
	const base = {
		isLoggedIn: false,
		isTransparent: false,
		menu: {} as AccountMenuState,
		notifContainerRef: createRef<HTMLDivElement>(),
		menuButtonRef: createRef<HTMLButtonElement>(),
		onNotificationNavigate: () => {},
	};

	it("labels the toggle 'Open menu' while the panel is closed", () => {
		renderWithProviders(
			<MobileHeader {...base} mobileOpen={false} setMobileOpen={() => {}} />,
		);

		const toggle = screen.getByRole("button", { name: "Open menu" });
		expect(toggle).toHaveAttribute("aria-expanded", "false");
	});

	it("labels the toggle 'Close menu' while the panel is open", () => {
		renderWithProviders(
			<MobileHeader {...base} mobileOpen={true} setMobileOpen={() => {}} />,
		);

		const toggle = screen.getByRole("button", { name: "Close menu" });
		expect(toggle).toHaveAttribute("aria-expanded", "true");
		expect(screen.queryByRole("button", { name: "Open menu" })).toBeNull();
	});
});
