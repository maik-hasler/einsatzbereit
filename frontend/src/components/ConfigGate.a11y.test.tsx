import { describe, it, vi, afterEach } from "vitest";
import ConfigGate from "./ConfigGate";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { mockRuntimeConfig } = vi.hoisted(() => ({
	mockRuntimeConfig: { isConfigured: false },
}));

vi.mock("../lib/runtimeConfig", () => ({ runtimeConfig: mockRuntimeConfig }));

function setNavigatorOnLine(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

describe("ConfigGate", () => {
	afterEach(() => {
		mockRuntimeConfig.isConfigured = false;
		setNavigatorOnLine(true);
	});

	it("has no a11y violations on its configuration-missing screen", async () => {
		const { container } = renderWithProviders(
			<ConfigGate>
				<p>protected content</p>
			</ConfigGate>,
		);

		await expectNoA11yViolations(container);
	});

	it("has no a11y violations on its offline screen", async () => {
		setNavigatorOnLine(false);

		const { container } = renderWithProviders(
			<ConfigGate>
				<p>protected content</p>
			</ConfigGate>,
		);

		await expectNoA11yViolations(container);
	});
});
