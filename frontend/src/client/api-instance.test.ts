import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

const dispatchToast = vi.fn();
const notifySessionExpired = vi.fn();
const clearAuthRecoveryAttempts = vi.fn();
const t = vi.fn((key: string, options?: Record<string, unknown>) =>
	options ? `${key}:${JSON.stringify(options)}` : key,
);

vi.mock("../lib/toastBus", () => ({
	dispatchToast: (level: string, message: string) =>
		dispatchToast(level, message),
}));
vi.mock("../lib/sessionExpiryBus", () => ({
	notifySessionExpired: () => notifySessionExpired(),
}));
vi.mock("../lib/authRecovery", () => ({
	clearAuthRecoveryAttempts: () => clearAuthRecoveryAttempts(),
}));
vi.mock("../i18n", () => ({
	// A rest/spread forward (rather than a fixed (key, options) signature)
	// preserves whether the real call site actually passed a second
	// argument - toHaveBeenCalledWith is exact about arity, and the source
	// calls t() with just one argument in the no-Retry-After-header path.
	default: { t: (...args: Parameters<typeof t>) => t(...args) },
}));

function makeResponse(status: number, headers: Record<string, string> = {}) {
	return new Response(null, { status, headers });
}

// rateLimitSuppressedUntil is module-level state in api-instance.ts, scoped
// to a single recovery episode by design (see the comment there) - reset
// the module between tests so that state doesn't leak across cases (see
// frontend/AGENTS.md's vi.resetModules() convention for module singletons).
async function loadHandleErrorResponse() {
	vi.resetModules();
	const module = await import("./api-instance");
	return module.handleErrorResponse;
}

beforeEach(() => {
	vi.clearAllMocks();
});

afterEach(() => {
	vi.useRealTimers();
});

describe("handleErrorResponse", () => {
	it("does nothing for a successful anonymous response", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(200), false);
		expect(clearAuthRecoveryAttempts).not.toHaveBeenCalled();
		expect(dispatchToast).not.toHaveBeenCalled();
	});

	it("clears the auth-recovery counter on a successful authenticated response", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(200), true);
		expect(clearAuthRecoveryAttempts).toHaveBeenCalledTimes(1);
	});

	it("notifies session expiry on a 401 that carried an access token", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(401), true);
		expect(notifySessionExpired).toHaveBeenCalledTimes(1);
	});

	it("ignores a 401 with no access token attached", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(401), false);
		expect(notifySessionExpired).not.toHaveBeenCalled();
	});

	it("toasts the forbidden message on 403", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(403), true);
		expect(dispatchToast).toHaveBeenCalledWith("error", "error.forbidden");
	});

	it("toasts a count-aware message using Retry-After on 429", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(429, { "Retry-After": "30" }), true);
		expect(t).toHaveBeenCalledWith("error.rateLimited", { count: 30 });
		expect(dispatchToast).toHaveBeenCalledTimes(1);
	});

	it("falls back to a generic message when Retry-After is missing on 429", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(429), true);
		expect(t).toHaveBeenCalledWith("error.rateLimitedGeneric");
	});

	it("falls back to a generic message when Retry-After is not a valid number", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(
			makeResponse(429, { "Retry-After": "not-a-number" }),
			true,
		);
		expect(t).toHaveBeenCalledWith("error.rateLimitedGeneric");
	});

	it("suppresses a repeat 429 toast within the same backoff window", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(429, { "Retry-After": "30" }), true);
		await handleErrorResponse(makeResponse(429, { "Retry-After": "30" }), true);
		expect(dispatchToast).toHaveBeenCalledTimes(1);
	});

	it("announces again once the backoff window has passed", async () => {
		vi.useFakeTimers();
		const handleErrorResponse = await loadHandleErrorResponse();

		await handleErrorResponse(makeResponse(429, { "Retry-After": "1" }), true);
		vi.advanceTimersByTime(1100);
		await handleErrorResponse(makeResponse(429, { "Retry-After": "1" }), true);

		expect(dispatchToast).toHaveBeenCalledTimes(2);
	});

	it("dispatches a server error toast on 5xx", async () => {
		const handleErrorResponse = await loadHandleErrorResponse();
		await handleErrorResponse(makeResponse(503), true);
		expect(dispatchToast).toHaveBeenCalledWith("error", "error.serverError");
	});
});
