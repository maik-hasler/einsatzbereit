import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import { useSessionExpiryHandler } from "./useSessionExpiryHandler";
import { useAuthDisplayStatus } from "./useAuthDisplayStatus";
import { notifySessionExpired } from "../lib/sessionExpiryBus";
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

const signinRedirect = vi.fn();

beforeEach(() => {
	vi.useFakeTimers({ shouldAdvanceTime: true });
	signinRedirect.mockReset().mockResolvedValue(undefined);
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
