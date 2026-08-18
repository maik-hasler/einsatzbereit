import { describe, expect, it } from "vitest";
import { isTerminalEngagementStatus } from "./engagementStatus";

describe("isTerminalEngagementStatus", () => {
	it("returns true for Cancelled and Withdrawn", () => {
		expect(isTerminalEngagementStatus("Cancelled")).toBe(true);
		expect(isTerminalEngagementStatus("Withdrawn")).toBe(true);
	});

	it("returns false for Pending and Confirmed", () => {
		expect(isTerminalEngagementStatus("Pending")).toBe(false);
		expect(isTerminalEngagementStatus("Confirmed")).toBe(false);
	});
});
