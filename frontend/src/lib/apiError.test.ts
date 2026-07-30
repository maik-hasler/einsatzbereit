import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

const { existsMock, tMock } = vi.hoisted(() => ({
	existsMock: vi.fn(),
	tMock: vi.fn(),
}));

vi.mock("../i18n", () => ({
	default: {
		exists: existsMock,
		t: tMock,
	},
}));

import {
	getApiErrorMessage,
	isApiErrorCode,
	isApiNotFoundError,
} from "./apiError";

describe("getApiErrorMessage", () => {
	beforeEach(() => {
		existsMock.mockReset();
		tMock.mockReset();
		vi.spyOn(console, "error").mockImplementation(() => undefined);
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	it("returns the fallback for a non-object error", () => {
		expect(getApiErrorMessage("boom", "fallback text")).toBe("fallback text");
	});

	it("returns the fallback for null", () => {
		expect(getApiErrorMessage(null, "fallback text")).toBe("fallback text");
	});

	it("returns the fallback when errorCode has no matching translation", () => {
		existsMock.mockReturnValue(false);
		const message = getApiErrorMessage(
			{ errorCode: "Unmapped.Code" },
			"fallback text",
		);
		expect(message).toBe("fallback text");
		expect(existsMock).toHaveBeenCalledWith("apiError.Unmapped.Code");
	});

	it("returns the translated message when errorCode has a matching key", () => {
		existsMock.mockReturnValue(true);
		tMock.mockReturnValue("Localized message");
		const message = getApiErrorMessage(
			{ errorCode: "Org.NotFound" },
			"fallback text",
		);
		expect(message).toBe("Localized message");
		expect(tMock).toHaveBeenCalledWith("apiError.Org.NotFound");
	});

	it("returns the fallback when errorCode is an empty string", () => {
		const message = getApiErrorMessage({ errorCode: "   " }, "fallback text");
		expect(message).toBe("fallback text");
		expect(existsMock).not.toHaveBeenCalled();
	});

	it("returns the fallback when there is no errorCode at all (e.g. network failure)", () => {
		const message = getApiErrorMessage({}, "fallback text");
		expect(message).toBe("fallback text");
	});

	it("logs a non-empty detail without exposing it in the returned message", () => {
		existsMock.mockReturnValue(false);
		const message = getApiErrorMessage(
			{ detail: "Internal SQL error at line 42" },
			"fallback text",
		);
		expect(message).toBe("fallback text");
		expect(console.error).toHaveBeenCalledWith(
			"[API] error detail (not shown to user):",
			"Internal SQL error at line 42",
		);
	});

	it("does not log when detail is only whitespace", () => {
		getApiErrorMessage({ detail: "   " }, "fallback text");
		expect(console.error).not.toHaveBeenCalled();
	});
});

describe("isApiNotFoundError", () => {
	it("returns true when status is 404", () => {
		expect(isApiNotFoundError({ status: 404 })).toBe(true);
	});

	it("returns false when status is a different code", () => {
		expect(isApiNotFoundError({ status: 500 })).toBe(false);
	});

	it("returns false when status is missing", () => {
		expect(isApiNotFoundError({})).toBe(false);
	});

	it("returns false for null", () => {
		expect(isApiNotFoundError(null)).toBe(false);
	});

	it("returns false for a non-object value", () => {
		expect(isApiNotFoundError("404")).toBe(false);
	});
});

describe("isApiErrorCode", () => {
	it("returns true when errorCode matches", () => {
		expect(
			isApiErrorCode(
				{ errorCode: "VolunteerOpportunity.AlreadyPublished" },
				"VolunteerOpportunity.AlreadyPublished",
			),
		).toBe(true);
	});

	it("returns false when errorCode is a different code", () => {
		expect(
			isApiErrorCode(
				{ errorCode: "VolunteerOpportunity.NotFound" },
				"VolunteerOpportunity.AlreadyPublished",
			),
		).toBe(false);
	});

	it("returns false when errorCode is missing", () => {
		expect(isApiErrorCode({}, "VolunteerOpportunity.AlreadyPublished")).toBe(
			false,
		);
	});

	it("returns false for null", () => {
		expect(isApiErrorCode(null, "VolunteerOpportunity.AlreadyPublished")).toBe(
			false,
		);
	});

	it("returns false for a non-object value", () => {
		expect(
			isApiErrorCode(
				"VolunteerOpportunity.AlreadyPublished",
				"VolunteerOpportunity.AlreadyPublished",
			),
		).toBe(false);
	});
});
