import { describe, expect, it } from "vitest";
import {
	getFeedbackEditDeadline,
	isFeedbackEditable,
	FEEDBACK_EDIT_WINDOW_DAYS,
} from "./feedback";

describe("isFeedbackEditable", () => {
	const now = new Date("2026-01-15T12:00:00Z");

	it("returns false when feedback was never submitted", () => {
		expect(isFeedbackEditable(null, now)).toBe(false);
		expect(isFeedbackEditable(undefined, now)).toBe(false);
	});

	it("returns true right after submission", () => {
		expect(isFeedbackEditable(now, now)).toBe(true);
	});

	it("returns true just before the window closes", () => {
		const submittedAt = new Date(now);
		submittedAt.setDate(submittedAt.getDate() - FEEDBACK_EDIT_WINDOW_DAYS);
		submittedAt.setMilliseconds(submittedAt.getMilliseconds() + 1);
		expect(isFeedbackEditable(submittedAt, now)).toBe(true);
	});

	it("returns false once the window has passed", () => {
		const submittedAt = new Date(now);
		submittedAt.setDate(submittedAt.getDate() - FEEDBACK_EDIT_WINDOW_DAYS - 1);
		expect(isFeedbackEditable(submittedAt, now)).toBe(false);
	});

	it("accepts an ISO date string as returned by the API client", () => {
		expect(isFeedbackEditable(now.toISOString(), now)).toBe(true);
	});
});

describe("getFeedbackEditDeadline", () => {
	const submittedAt = new Date("2026-01-15T12:00:00Z");

	it("is the submission date plus the edit window", () => {
		const deadline = getFeedbackEditDeadline(submittedAt);

		expect(deadline?.getTime()).toBe(
			submittedAt.getTime() + FEEDBACK_EDIT_WINDOW_DAYS * 24 * 60 * 60 * 1000,
		);
	});

	it("is null when feedback was never submitted", () => {
		expect(getFeedbackEditDeadline(null)).toBeNull();
		expect(getFeedbackEditDeadline(undefined)).toBeNull();
	});
});
