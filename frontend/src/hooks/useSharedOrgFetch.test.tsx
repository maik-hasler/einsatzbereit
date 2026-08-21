import { describe, it, expect, vi, beforeEach } from "vitest";
import { waitFor } from "@testing-library/react";
import { useMyOrganizations } from "./useMyOrganizations";
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
});
