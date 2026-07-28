import { describe, expect, it, vi } from "vitest";
import { isTransientApiError, withRetry } from "./retry";

describe("isTransientApiError", () => {
	it("treats a rejection with no numeric status as transient", () => {
		expect(isTransientApiError(new TypeError("Failed to fetch"))).toBe(true);
		expect(isTransientApiError({ status: "500" })).toBe(true);
		expect(isTransientApiError(null)).toBe(true);
	});

	it("treats 429 and 5xx responses as transient", () => {
		expect(isTransientApiError({ status: 429 })).toBe(true);
		expect(isTransientApiError({ status: 500 })).toBe(true);
		expect(isTransientApiError({ status: 503 })).toBe(true);
	});

	it("treats other 4xx responses as final", () => {
		expect(isTransientApiError({ status: 400 })).toBe(false);
		expect(isTransientApiError({ status: 401 })).toBe(false);
		expect(isTransientApiError({ status: 403 })).toBe(false);
		expect(isTransientApiError({ status: 404 })).toBe(false);
	});
});

describe("withRetry", () => {
	it("returns the first successful result without retrying", async () => {
		const operation = vi.fn().mockResolvedValue("ok");

		await expect(withRetry(operation, { delayMs: 0 })).resolves.toBe("ok");
		expect(operation).toHaveBeenCalledTimes(1);
	});

	// The regression this whole module exists for: one dropped request used to
	// leave HomePage permanently on its "no organizations" branch.
	it("recovers when a transient failure is followed by a success", async () => {
		const operation = vi
			.fn()
			.mockRejectedValueOnce(new TypeError("Failed to fetch"))
			.mockResolvedValue(["org"]);

		await expect(withRetry(operation, { delayMs: 0 })).resolves.toEqual([
			"org",
		]);
		expect(operation).toHaveBeenCalledTimes(2);
	});

	it("gives up after the attempt budget and rethrows the last error", async () => {
		const error = { status: 503 };
		const operation = vi.fn().mockRejectedValue(error);

		await expect(
			withRetry(operation, { attempts: 3, delayMs: 0 }),
		).rejects.toBe(error);
		expect(operation).toHaveBeenCalledTimes(3);
	});

	it("does not retry a failure that cannot succeed on a repeat", async () => {
		const error = { status: 403 };
		const operation = vi.fn().mockRejectedValue(error);

		await expect(withRetry(operation, { delayMs: 0 })).rejects.toBe(error);
		expect(operation).toHaveBeenCalledTimes(1);
	});

	it("honours a caller-supplied shouldRetry predicate", async () => {
		const operation = vi.fn().mockRejectedValue({ status: 404 });

		await expect(
			withRetry(operation, {
				attempts: 2,
				delayMs: 0,
				shouldRetry: () => true,
			}),
		).rejects.toEqual({ status: 404 });
		expect(operation).toHaveBeenCalledTimes(2);
	});
});
