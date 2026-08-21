import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LocationSearchInput from "./LocationSearchInput";
import { renderWithProviders } from "../test/render";

/**
 * `CityExactNameMatchSuggestionTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * #1930: a suggestion whose label is exactly what was typed renders
 * identically to any other result, so it reads as the raw query echoed back as
 * a fake, selectable "place". The caption is the only thing distinguishing it;
 * selecting it still geocodes normally.
 *
 * The E2E needed a backend `FakeGeocodingService` to produce a predictable
 * result set. Here it is one mocked `searchCities` response.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const place = (label: string) => ({ label, latitude: 54.32, longitude: 10.13 });

beforeEach(() => {
	api.__reset();
});

/**
 * Controlled input, so typing has to feed `value` back in - the suggestion
 * list and the exact-match comparison both read it.
 */
function Harness({ onSelect }: { onSelect: (s: unknown) => void }) {
	const [value, setValue] = useState("");
	return (
		<LocationSearchInput
			id="city"
			value={value}
			onValueChange={setValue}
			onSelect={onSelect}
			placeholder="City"
			ariaLabel="City"
			inputClassName=""
		/>
	);
}

describe("LocationSearchInput exact-name match", () => {
	it("captions the suggestion that is exactly what was typed", async () => {
		api.searchCities.mockResolvedValue([place("Kiel"), place("Kiel-Holtenau")]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Kiel");

		const options = await screen.findAllByRole("option");
		expect(options).toHaveLength(2);
		// Only the exact one is captioned - otherwise the caption says nothing.
		expect(within(options[0]).getByText("Matches exactly")).toBeInTheDocument();
		expect(within(options[1]).queryByText("Matches exactly")).toBeNull();
	});

	it("still selects it like any other suggestion", async () => {
		// The caption clarifies what the row is; it does not make it inert.
		api.searchCities.mockResolvedValue([place("Kiel")]);
		const onSelect = vi.fn();

		renderWithProviders(<Harness onSelect={onSelect} />);

		await userEvent.type(screen.getByLabelText("City"), "Kiel");
		await userEvent.click((await screen.findAllByRole("option"))[0]);

		expect(onSelect).toHaveBeenCalledWith(
			expect.objectContaining({ label: "Kiel", lat: 54.32, lng: 10.13 }),
		);
	});
});
