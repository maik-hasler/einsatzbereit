import { describe, it, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import QRScannerModal from "./QRScannerModal";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = vi.hoisted(() => ({
	api: {
		checkInEngagement: vi.fn(),
		checkInEngagementByCode: vi.fn(),
	},
}));

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const OPPORTUNITY_ID = "opp-1";
const originalMediaDevices = navigator.mediaDevices;

beforeEach(() => {
	vi.clearAllMocks();
});

afterEach(() => {
	Object.defineProperty(navigator, "mediaDevices", {
		configurable: true,
		value: originalMediaDevices,
	});
});

function open() {
	return renderWithProviders(
		<QRScannerModal
			opportunityId={OPPORTUNITY_ID}
			onCheckedIn={() => {}}
			onClose={() => {}}
		/>,
	);
}

describe("QRScannerModal a11y", () => {
	it("has no violations while camera support is still being detected", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});
		open();
		await expectNoA11yViolations();
	});

	it("has no violations when no camera API is available", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});
		open();
		await screen.findByText(/QR scanning isn't available/);
		await expectNoA11yViolations();
	});

	it("has no violations showing the live camera view", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [] }),
			},
		});
		open();
		await screen.findByLabelText("Camera view for QR code scanning");
		await expectNoA11yViolations();
	});

	it("has no violations when camera access is denied", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockRejectedValue(new Error("denied")),
			},
		});
		open();
		await screen.findByText(/Camera access denied/);
		await expectNoA11yViolations();
	});

	it("has no violations when the fallback code was rejected", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [] }),
			},
		});
		api.checkInEngagementByCode.mockRejectedValue(new Error("nope"));
		open();

		await userEvent.type(
			await screen.findByLabelText("Volunteer's check-in code"),
			"abcd1234",
		);
		await userEvent.click(screen.getByRole("button", { name: "Check in" }));

		await screen.findByText("Check-in failed. Please try again.");
		await expectNoA11yViolations();
	});

	it("has no violations after a successful fallback code check-in", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});
		api.checkInEngagementByCode.mockResolvedValue({
			id: "abcd1234-0000-0000-0000-000000000000",
			status: "Confirmed",
		});
		open();

		await userEvent.type(
			await screen.findByLabelText("Volunteer's check-in code"),
			"abcd1234",
		);
		await userEvent.click(screen.getByRole("button", { name: "Check in" }));

		await screen.findByText("Volunteer checked in successfully!");
		await expectNoA11yViolations();
	});
});
