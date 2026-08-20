import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, screen, waitFor } from "@testing-library/react";
import { useSessionExpiryHandler } from "./useSessionExpiryHandler";
import { notifySessionExpired } from "../lib/sessionExpiryBus";
import { renderWithProviders } from "../test/render";

/**
 * Was four of `SessionExpiryTests`' five cases, moved down in #2148 wave 3.
 *
 * Each of them signed a volunteer in, intercepted every authenticated GET
 * with a 401, and then asserted which of two things happened: a toast, or
 * nothing. The 401 interception is what `sessionExpiryBus` already reduces to
 * a single call, so the whole setup collapses to one `notifySessionExpired()`.
 *
 * `AuthenticatedRequest_Returns401_RedirectsToKeycloakSignIn` stays
 * end-to-end: its assertion is that the redirect lands on Keycloak's real
 * `/protocol/openid-connect/auth` page, which only a browser can see.
 */
function Harness() {
	useSessionExpiryHandler();
	return <div>App body</div>;
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
		// Firing signinRedirect immediately raced the toast's paint against
		// Keycloak's top-level navigation, which occasionally won - the toast
		// never became visible even though the navigation itself was fine.
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
		// A stale user object can sit in sessionStorage from an earlier login,
		// and automaticSilentRenew fires a doomed renewal for it on mount
		// whatever page is open. That is not "your session just expired" - there
		// is nothing to interrupt.
		renderWithProviders(<Harness />, { auth: { isAuthenticated: false } });

		act(() => notifySessionExpired());

		await act(async () => {
			await vi.advanceTimersByTimeAsync(2100);
		});
		await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
		expect(signinRedirect).not.toHaveBeenCalled();
	});
});
