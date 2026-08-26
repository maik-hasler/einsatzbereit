import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import { useSessionExpiryHandler } from "./useSessionExpiryHandler";
import { useAuthDisplayStatus } from "./useAuthDisplayStatus";
import { notifySessionExpired } from "../lib/sessionExpiryBus";
import { clearAuthRecoveryAttempts } from "../lib/authRecovery";
import { useAuthRecoveryFailedFlag } from "../contexts/AuthStatusContext";
import { renderWithProviders } from "../test/render";

function Harness() {
	useSessionExpiryHandler();
	return <div>App body</div>;
}

function StatusHarness() {
	useSessionExpiryHandler();
	const status = useAuthDisplayStatus();
	return <div data-testid="auth-display-status">{status}</div>;
}

function RecoveryHarness() {
	useSessionExpiryHandler();
	const recoveryFailed = useAuthRecoveryFailedFlag();
	return <div data-testid="recovery-failed">{String(recoveryFailed)}</div>;
}

const signinRedirect = vi.fn();

beforeEach(() => {
	vi.useFakeTimers({ shouldAdvanceTime: true });
	signinRedirect.mockReset().mockResolvedValue(undefined);
	sessionStorage.clear();
});

afterEach(() => {
	vi.useRealTimers();
});

describe("session expiry", () => {
	it("announces the expiry as a toast for a signed-in visitor", async () => {
		renderWithProviders(<Harness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});

		act(() => notifySessionExpired());

		const toast = await screen.findByRole("alert");
		expect(toast).toHaveTextContent(/session/i);
	});

	it("holds the toast on screen before handing over to the sign-in redirect", async () => {
		renderWithProviders(<Harness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});

		act(() => notifySessionExpired());
		await screen.findByRole("alert");
		expect(signinRedirect).not.toHaveBeenCalled();

		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		expect(signinRedirect).toHaveBeenCalledTimes(1);
	});

	it("acts once when several concurrent calls all report the same expiry", async () => {
		renderWithProviders(<Harness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});

		act(() => {
			notifySessionExpired();
			notifySessionExpired();
			notifySessionExpired();
		});

		expect(screen.getAllByRole("alert")).toHaveLength(1);
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		expect(signinRedirect).toHaveBeenCalledTimes(1);
	});

	it("stays quiet for a visitor who was never signed in", async () => {
		renderWithProviders(<Harness />, { auth: { isAuthenticated: false } });

		act(() => notifySessionExpired());

		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
		expect(signinRedirect).not.toHaveBeenCalled();
	});

	it("flips the shared auth display status to sessionExpired instead of signedOut (#2224)", async () => {
		renderWithProviders(<StatusHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});

		expect(screen.getByTestId("auth-display-status")).toHaveTextContent(
			"signedIn",
		);

		act(() => notifySessionExpired());

		await waitFor(() =>
			expect(screen.getByTestId("auth-display-status")).toHaveTextContent(
				"sessionExpired",
			),
		);
	});
});

describe("bounded auth recovery (#2208)", () => {
	it("still redirects on the first expiry of a fresh recovery episode", async () => {
		renderWithProviders(<RecoveryHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});

		act(() => notifySessionExpired());
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});

		expect(signinRedirect).toHaveBeenCalledTimes(1);
		expect(screen.getByTestId("recovery-failed")).toHaveTextContent("false");
	});

	it("gives up instead of redirecting again on a second consecutive expiry across a reload", async () => {
		// A real redirect round trip is a full-page navigation, which remounts
		// the whole app - simulate that here by unmounting and rendering a
		// fresh instance, while the sessionStorage-backed counter survives.
		const first = renderWithProviders(<RecoveryHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});
		act(() => notifySessionExpired());
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		expect(signinRedirect).toHaveBeenCalledTimes(1);
		first.unmount();

		renderWithProviders(<RecoveryHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});
		act(() => notifySessionExpired());
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});

		expect(signinRedirect).toHaveBeenCalledTimes(1);
		expect(screen.getByTestId("recovery-failed")).toHaveTextContent("true");
	});

	it("gets a fresh redirect attempt once a successful API call clears the counter", async () => {
		const first = renderWithProviders(<RecoveryHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});
		act(() => notifySessionExpired());
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		expect(signinRedirect).toHaveBeenCalledTimes(1);
		first.unmount();

		// Stands in for api-instance.ts's own clear-on-success call.
		clearAuthRecoveryAttempts();

		renderWithProviders(<RecoveryHarness />, {
			auth: { isAuthenticated: true, signinRedirect },
		});
		act(() => notifySessionExpired());
		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});

		expect(signinRedirect).toHaveBeenCalledTimes(2);
		expect(screen.getByTestId("recovery-failed")).toHaveTextContent("false");
	});
});
