import { describe, it, vi } from "vitest";
import PwaUpdatePrompt from "./PwaUpdatePrompt";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { useRegisterSW } = vi.hoisted(() => ({ useRegisterSW: vi.fn() }));

vi.mock("virtual:pwa-register/react", () => ({ useRegisterSW }));

describe("PwaUpdatePrompt", () => {
	it("has no a11y violations while showing the reload prompt", async () => {
		useRegisterSW.mockReturnValue({
			needRefresh: [true, vi.fn()],
			offlineReady: [false, vi.fn()],
			updateServiceWorker: vi.fn(),
		});

		const { container } = renderWithProviders(<PwaUpdatePrompt />);

		await expectNoA11yViolations(container);
	});
});
