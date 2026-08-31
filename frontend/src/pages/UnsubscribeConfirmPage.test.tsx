import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Route, Routes, useLocation } from "react-router";
import UnsubscribeConfirmPage from "./UnsubscribeConfirmPage";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const USER_ID = "11111111-1111-1111-1111-111111111111";
const TOKEN = "22222222-2222-2222-2222-222222222222";

function CurrentPath() {
	const location = useLocation();
	return <div data-testid="path">{location.pathname}</div>;
}

function renderConfirm(query: string) {
	return renderWithProviders(
		<>
			<Routes>
				<Route path="/unsubscribe" element={<UnsubscribeConfirmPage />} />
				<Route path="/unsubscribed" element={<p>Unsubscribed</p>} />
			</Routes>
			<CurrentPath />
		</>,
		{ lng: "en", route: `/unsubscribe${query}` },
	);
}

const validQuery = `?userId=${USER_ID}&type=EngagementReminder&token=${TOKEN}`;

beforeEach(() => {
	api.__reset();
});

describe("UnsubscribeConfirmPage confirming", () => {
	it("unsubscribes through the API and stays inside the app", async () => {
		api.unsubscribe.mockResolvedValue(undefined);
		renderConfirm(validQuery);

		const confirm = screen.getByRole("button", {
			name: "Confirm unsubscribe",
		});
		// Never a link to the API origin: that click used to leave the SPA.
		expect(screen.queryByRole("link", { name: /confirm/i })).toBeNull();

		await userEvent.click(confirm);

		await waitFor(() =>
			expect(screen.getByTestId("path")).toHaveTextContent("/unsubscribed"),
		);
		expect(api.unsubscribe).toHaveBeenCalledWith(
			USER_ID,
			"EngagementReminder",
			TOKEN,
		);
	});

	it("keeps a rejected token on the page, with a message and a way on", async () => {
		api.unsubscribe.mockRejectedValue({ status: 403 });
		renderConfirm(validQuery);

		await userEvent.click(
			screen.getByRole("button", { name: "Confirm unsubscribe" }),
		);

		expect(await screen.findByRole("alert")).toBeInTheDocument();
		expect(screen.getByTestId("path")).toHaveTextContent("/unsubscribe");
		expect(
			screen.getByRole("button", { name: "Confirm unsubscribe" }),
		).toBeEnabled();
	});
});

describe("UnsubscribeConfirmPage a link it cannot act on", () => {
	it.each([
		["a missing link", ""],
		[
			"a type that is not a real notification type",
			`?userId=${USER_ID}&type=NotARealType&token=${TOKEN}`,
		],
		[
			"a token that is not a GUID",
			`?userId=${USER_ID}&type=Withdrawal&token=not-a-guid`,
		],
	])("offers a way back for %s", async (_name, query) => {
		renderConfirm(query);

		expect(
			screen.getByRole("heading", {
				level: 1,
				name: "This unsubscribe link doesn't work",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByRole("link", { name: "Manage notification preferences" }),
		).toHaveAttribute("href", "/profile");
		expect(screen.getByRole("link", { name: "Back to home" })).toHaveAttribute(
			"href",
			"/",
		);
		expect(
			screen.queryByRole("button", { name: "Confirm unsubscribe" }),
		).toBeNull();
	});

	it("never echoes the raw type query param into the copy", () => {
		const long = "x".repeat(400);
		renderConfirm(`?userId=${USER_ID}&type=${long}&token=${TOKEN}`);

		expect(document.body.textContent).not.toContain(long);
	});
});
