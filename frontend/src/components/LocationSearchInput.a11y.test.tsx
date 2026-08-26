import { describe, it, beforeEach, vi } from "vitest";
import { useState } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LocationSearchInput from "./LocationSearchInput";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const place = (label: string) => ({ label, latitude: 54.32, longitude: 10.13 });

beforeEach(() => {
	api.__reset();
});

function Harness() {
	const [value, setValue] = useState("");
	return (
		<LocationSearchInput
			id="city"
			value={value}
			onValueChange={setValue}
			onSelect={() => {}}
			placeholder="City"
			ariaLabel="City"
			inputClassName=""
		/>
	);
}

describe("LocationSearchInput a11y", () => {
	it("has no violations in its resting state", async () => {
		renderWithProviders(<Harness />);
		await expectNoA11yViolations();
	});

	it("has no violations while the query is too short to be a confident no-match", async () => {
		api.searchCities.mockResolvedValue([]);
		renderWithProviders(<Harness />);

		await userEvent.type(screen.getByLabelText("City"), "Le");
		await screen.findByText("Keep typing…");

		await expectNoA11yViolations();
	});

	it("has no violations once the query is long enough to assert no match", async () => {
		api.searchCities.mockResolvedValue([]);
		renderWithProviders(<Harness />);

		await userEvent.type(screen.getByLabelText("City"), "Xyz");
		await screen.findByText("No matching city found.");

		await expectNoA11yViolations();
	});

	it("has no violations with the suggestion list open", async () => {
		api.searchCities.mockResolvedValue([place("Kiel"), place("Kiel-Holtenau")]);
		renderWithProviders(<Harness />);

		await userEvent.type(screen.getByLabelText("City"), "Kiel");
		await screen.findAllByRole("option");

		await expectNoA11yViolations();
	});

	it("has no violations when the search failed", async () => {
		api.searchCities.mockRejectedValue(new Error("network error"));
		renderWithProviders(<Harness />);

		// A different query than the other cases above - useCitySuggestions
		// caches successful results at module scope keyed by query text, and
		// this test must not be served that cached "Kiel" suggestion list
		// instead of actually exercising the rejected mock.
		await userEvent.type(screen.getByLabelText("City"), "Bremen");
		await screen.findByText("Couldn't search for that city. Please try again.");

		await expectNoA11yViolations();
	});
});
