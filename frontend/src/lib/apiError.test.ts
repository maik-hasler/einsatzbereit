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
	getApiErrorStatus,
	isApiErrorCode,
	isApiForbiddenError,
	isApiNotFoundError,
	isNetworkError,
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

describe("getApiErrorStatus", () => {
	it("reads the status off a ProblemDetails-shaped rejection", () => {
		expect(getApiErrorStatus({ status: 403, errorCode: "Org.Forbidden" })).toBe(
			403,
		);
	});

	// The case #1774 turns on: NSwag emits no 400 branch for
	// GetOrganizationDetails, so an all-zero organization id (rejected by
	// OrganizationId.Create) arrives as a bare ApiException whose only usable
	// signal is this status.
	it("reads the status off an ApiException-shaped rejection carrying no errorCode", () => {
		expect(getApiErrorStatus({ status: 400, response: "{}" })).toBe(400);
	});

	it("returns null when the rejection carries no status (e.g. a network failure)", () => {
		expect(getApiErrorStatus(new TypeError("Failed to fetch"))).toBeNull();
	});

	it("returns null when status is not a number", () => {
		expect(getApiErrorStatus({ status: "404" })).toBeNull();
	});

	it("returns null for null", () => {
		expect(getApiErrorStatus(null)).toBeNull();
	});

	it("returns null for a non-object value", () => {
		expect(getApiErrorStatus("500")).toBeNull();
	});
});

describe("isNetworkError", () => {
	// #1901: a fetch that never reached the server (dropped connection, DNS
	// failure) is the one case getApiErrorStatus itself cannot distinguish
	// from "an error response happened to omit its status" - this makes that
	// distinction available under its own name for callers that specifically
	// want to treat "no HTTP response at all" as an offline signal.
	it("returns true for a rejection with no status (e.g. a network failure)", () => {
		expect(isNetworkError(new TypeError("Failed to fetch"))).toBe(true);
	});

	it("returns true for null", () => {
		expect(isNetworkError(null)).toBe(true);
	});

	it("returns false for a ProblemDetails-shaped rejection carrying a status", () => {
		expect(isNetworkError({ status: 500, errorCode: "Server.Error" })).toBe(
			false,
		);
	});

	it("returns false for an ApiException-shaped rejection carrying a status", () => {
		expect(isNetworkError({ status: 403, response: "{}" })).toBe(false);
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

describe("isApiForbiddenError", () => {
	it("returns true when status is 403", () => {
		expect(isApiForbiddenError({ status: 403 })).toBe(true);
	});

	it("returns false when status is a different code", () => {
		expect(isApiForbiddenError({ status: 500 })).toBe(false);
	});

	it("returns false when status is missing", () => {
		expect(isApiForbiddenError({})).toBe(false);
	});

	it("returns false for null", () => {
		expect(isApiForbiddenError(null)).toBe(false);
	});

	it("returns false for a non-object value", () => {
		expect(isApiForbiddenError("403")).toBe(false);
	});
});
