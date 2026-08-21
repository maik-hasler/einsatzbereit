import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import QRScannerModal from "./QRScannerModal";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

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
		<QRScannerModal onCheckedIn={() => {}} onClose={() => {}} />,
		{ auth: { isAuthenticated: true } },
	);

describe("QRScannerModal in a browser that cannot scan", () => {
	it("says so, and names a browser that can", async () => {
		renderScanner();

		expect(
			await screen.findByText(/QR scanning is not supported in this browser/),
		).toBeVisible();
		expect(
			screen.queryByLabelText("Camera view for QR code scanning"),
		).toBeNull();
	});
});

describe("QRScannerModal when camera permission is denied", () => {
	it("surfaces the denial instead of a blank frame", async () => {
		(globalThis as Record<string, unknown>).BarcodeDetector = class {
			detect() {
				return Promise.resolve([]);
			}
		};
		Object.defineProperty(navigator, "mediaDevices", {
			configurable: true,
			value: {
				getUserMedia: vi.fn().mockRejectedValue(new Error("NotAllowedError")),
			},
		});

		renderScanner();

		expect(await screen.findByText(/Camera access denied/)).toBeVisible();
		expect(screen.queryByText(/not supported in this browser/)).toBeNull();
		expect(
			screen.queryByLabelText("Camera view for QR code scanning"),
		).toBeNull();
	});
});
