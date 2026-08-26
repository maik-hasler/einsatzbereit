import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import QRScannerModal from "./QRScannerModal";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const OPPORTUNITY_ID = "22222222-2222-2222-2222-222222222222";
const originalMediaDevices = navigator.mediaDevices;

beforeEach(() => {
	api.__reset();
});

afterEach(() => {
	delete (globalThis as Record<string, unknown>).BarcodeDetector;
	Object.defineProperty(navigator, "mediaDevices", {
		configurable: true,
		value: originalMediaDevices,
	});
});

const renderScanner = () =>
	renderWithProviders(
		<QRScannerModal
			opportunityId={OPPORTUNITY_ID}
			onCheckedIn={() => {}}
			onClose={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);

describe("QRScannerModal without a camera API at all", () => {
	it("says so, and doesn't blame a specific browser", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});

		renderScanner();

		expect(
			await screen.findByText(/QR scanning isn't available on this device/),
		).toBeVisible();
		expect(screen.queryByText(/Chrome or Edge/)).toBeNull();
		expect(
			screen.queryByLabelText("Camera view for QR code scanning"),
		).toBeNull();
	});

	it("still offers the fallback code input", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});

		renderScanner();

		expect(
			await screen.findByLabelText("Volunteer's check-in code"),
		).toBeVisible();
	});
});

describe("QRScannerModal without a native BarcodeDetector", () => {
	it("still shows the live camera view instead of the unsupported error", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockResolvedValue({
					getTracks: () => [],
				}),
			},
		});

		renderScanner();

		expect(
			await screen.findByLabelText("Camera view for QR code scanning"),
		).toBeVisible();
		expect(screen.queryByText(/not available on this device/)).toBeNull();
	});
});

describe("QRScannerModal when camera permission is denied", () => {
	it("surfaces the denial instead of a blank frame", async () => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockRejectedValue(new Error("NotAllowedError")),
			},
		});

		renderScanner();

		expect(await screen.findByText(/Camera access denied/)).toBeVisible();
		expect(screen.queryByText(/not available on this device/)).toBeNull();
		expect(
			screen.queryByLabelText("Camera view for QR code scanning"),
		).toBeNull();
	});
});

describe("QRScannerModal fallback code input", () => {
	beforeEach(() => {
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: undefined,
		});
	});

	it("filters non-hex characters and lowercases as the organizer types", async () => {
		renderScanner();

		const input = await screen.findByLabelText("Volunteer's check-in code");
		await userEvent.type(input, "ABcd12-XY34ef56");

		expect((input as HTMLInputElement).value).toBe("abcd1234");
	});

	it("disables submit until exactly 8 characters are entered", async () => {
		renderScanner();

		const input = await screen.findByLabelText("Volunteer's check-in code");
		const submit = screen.getByRole("button", { name: "Check in" });
		expect(submit).toBeDisabled();

		await userEvent.type(input, "abcd123");
		expect(submit).toBeDisabled();

		await userEvent.type(input, "4");
		expect(submit).toBeEnabled();
	});

	it("checks in and shows success on a matching code", async () => {
		api.checkInEngagementByCode.mockResolvedValue({
			id: "abcd1234-0000-0000-0000-000000000000",
			status: "Confirmed",
		});
		const onCheckedIn = vi.fn();
		renderWithProviders(
			<QRScannerModal
				opportunityId={OPPORTUNITY_ID}
				onCheckedIn={onCheckedIn}
				onClose={() => {}}
			/>,
			{ auth: { isAuthenticated: true } },
		);

		await userEvent.type(
			await screen.findByLabelText("Volunteer's check-in code"),
			"abcd1234",
		);
		await userEvent.click(screen.getByRole("button", { name: "Check in" }));

		expect(
			await screen.findByText("Volunteer checked in successfully!"),
		).toBeVisible();
		expect(api.checkInEngagementByCode).toHaveBeenCalledWith(OPPORTUNITY_ID, {
			code: "abcd1234",
		});
		expect(onCheckedIn).toHaveBeenCalledWith(
			"abcd1234-0000-0000-0000-000000000000",
		);
	});

	it("shows a translated error when the code doesn't match", async () => {
		api.checkInEngagementByCode.mockRejectedValue({
			status: 404,
			errorCode: "Engagement.NotFound",
		});
		renderScanner();

		await userEvent.type(
			await screen.findByLabelText("Volunteer's check-in code"),
			"deadbeef",
		);
		await userEvent.click(screen.getByRole("button", { name: "Check in" }));

		expect(await screen.findByText("Sign-up not found.")).toBeVisible();
	});
});
