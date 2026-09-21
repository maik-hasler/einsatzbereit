import { describe, it, expect, vi, beforeEach } from "vitest";
import { waitFor, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import {
	useMyOrganizations,
	useInvalidateMyOrganizations,
} from "./useMyOrganizations";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("./useApiClient", () => ({ useApiClient: () => api }));

function Consumer({ label }: { label: string }) {
	const { orgs } = useMyOrganizations();
	return <span data-testid={label}>{orgs.length}</span>;
}

function Invalidator() {
	const invalidate = useInvalidateMyOrganizations();
	return (
		<button type="button" onClick={() => void invalidate()}>
			invalidate
		</button>
	);
}

beforeEach(() => {
	api.__reset();
	api.getOrganizations.mockResolvedValue([]);
});

describe("useMyOrganizations", () => {
	it("issues one request when several components ask on the same mount", async () => {
		renderWithProviders(
			<>
				<Consumer label="header" />
				<Consumer label="page" />
				<Consumer label="settings" />
			</>,
			{ auth: { isAuthenticated: true } },
		);

		await waitFor(() => expect(api.getOrganizations).toHaveBeenCalled());
		expect(api.getOrganizations).toHaveBeenCalledTimes(1);
	});

	it("asks for nothing at all when signed out", async () => {
		renderWithProviders(<Consumer label="anonymous" />);

		await waitFor(() => expect(api.getOrganizations).not.toHaveBeenCalled());
	});

	// The regression this guards: the list used to be keyed by a bare string
	// outside `queryKeys`, so `invalidateQueries({ queryKey:
	// queryKeys.organizations.all })` did not reach it. Creating an
	// organization then left the switcher resolving the active one out of a
	// list that predated it, and it rendered the *previously* active
	// organization's name for a full staleTime (20 VisualTests cases across
	// three shards).
	it("refetches the list when a mutation invalidates the organizations key", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<>
				<Consumer label="header" />
				<Invalidator />
			</>,
			{ auth: { isAuthenticated: true } },
		);

		await waitFor(() => expect(api.getOrganizations).toHaveBeenCalledTimes(1));

		await user.click(screen.getByRole("button", { name: "invalidate" }));

		await waitFor(() => expect(api.getOrganizations).toHaveBeenCalledTimes(2));
	});
});
