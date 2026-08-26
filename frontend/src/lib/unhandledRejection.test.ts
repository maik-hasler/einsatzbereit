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

import { handleUnhandledRejection } from "./unhandledRejection";
import { subscribeToasts, type ToastEvent } from "./toastBus";

describe("handleUnhandledRejection", () => {
	let toasts: ToastEvent[];
	let unsubscribe: () => void;

	beforeEach(() => {
		existsMock.mockReset();
		tMock.mockReset();
		tMock.mockImplementation((key: string) => key);
		toasts = [];
		unsubscribe = subscribeToasts((event) => toasts.push(event));
		vi.spyOn(console, "error").mockImplementation(() => undefined);
	});

	afterEach(() => {
		unsubscribe();
		vi.restoreAllMocks();
	});

	it("logs but does not toast a rejection with no errorCode (e.g. a missed .catch())", () => {
		handleUnhandledRejection(new TypeError("something exploded"));

		expect(console.error).toHaveBeenCalledWith(
			"[unhandledrejection]",
			expect.any(TypeError),
		);
		expect(toasts).toHaveLength(0);
	});

	it("logs but does not toast a plain network failure", () => {
		handleUnhandledRejection({ status: null });

		expect(toasts).toHaveLength(0);
	});

	it("logs every rejection unconditionally, actionable or not", () => {
		handleUnhandledRejection("a string rejection");
		expect(console.error).toHaveBeenCalledWith(
			"[unhandledrejection]",
			"a string rejection",
		);
	});

	it("toasts a translated, user-actionable message when errorCode has a known translation", () => {
		existsMock.mockReturnValue(true);
		tMock.mockReturnValue("This request was incomplete.");

		handleUnhandledRejection({ errorCode: "AchievementId.Empty" });

		expect(toasts).toHaveLength(1);
		expect(toasts[0].level).toBe("error");
		expect(toasts[0].message).toBe("This request was incomplete.");
		expect(tMock).toHaveBeenCalledWith("apiError.AchievementId.Empty");
	});

	it("does not toast when errorCode has no matching translation", () => {
		existsMock.mockReturnValue(false);

		handleUnhandledRejection({ errorCode: "Unmapped.Code" });

		expect(toasts).toHaveLength(0);
	});
});
