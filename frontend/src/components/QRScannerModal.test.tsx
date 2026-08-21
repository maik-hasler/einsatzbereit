import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen } from "@testing-library/react";
import QRScannerModal from "./QRScannerModal";
import { renderWithProviders } from "../test/render";

/**
 * `QRScannerModalTests`' two capability branches, moved down in #2148 wave 13.
 * Remaining inventory: #2159.
 *
 * Both are about what the component does when the *browser* cannot scan, and
 * both were already being asserted against a Chromium that lacks
 * `BarcodeDetector` - the class's own doc comment says so. jsdom lacks it in
 * exactly the same way, so the unsupported branch needs no stubbing at all,
 * and the denied-permission branch needs only a `BarcodeDetector` stub (to
 * flip `supported` true) plus a rejecting `getUserMedia`. No video element, no
 * stream, no timers: when `cameraError` is set the `<video>` is not rendered.
 */
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
		// `supported` is `typeof BarcodeDetector !== "undefined" &&
		// !!navigator.mediaDevices?.getUserMedia`. jsdom has neither, which is
		// the branch under test.
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
		// The unsupported message is the wrong diagnosis here - the browser can
		// scan, the user just said no.
		expect(screen.queryByText(/not supported in this browser/)).toBeNull();
		expect(
			screen.queryByLabelText("Camera view for QR code scanning"),
		).toBeNull();
	});
});
