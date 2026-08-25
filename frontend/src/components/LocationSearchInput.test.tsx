import { describe, it, expect, vi, beforeEach } from "vitest";
import { useState } from "react";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import LocationSearchInput from "./LocationSearchInput";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const place = (label: string) => ({ label, latitude: 54.32, longitude: 10.13 });

beforeEach(() => {
	api.__reset();
});

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

describe("LocationSearchInput empty-result messaging", () => {
	it("hints to keep typing rather than asserting no match for a short, still-incomplete query", async () => {
		api.searchCities.mockResolvedValue([]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Le");

		expect(await screen.findByRole("status")).toHaveTextContent("Keep typing");
	});

	it("asserts no match once the query is long enough to be confident", async () => {
		api.searchCities.mockResolvedValue([]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Xyz");

		expect(await screen.findByRole("status")).toHaveTextContent(
			"No matching city found.",
		);
	});
});

describe("LocationSearchInput exact-name match", () => {
	it("captions the suggestion that is exactly what was typed", async () => {
		api.searchCities.mockResolvedValue([place("Kiel"), place("Kiel-Holtenau")]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Kiel");

		const options = await screen.findAllByRole("option");
		expect(options).toHaveLength(2);
		expect(within(options[0]).getByText("Matches exactly")).toBeInTheDocument();
		expect(within(options[1]).queryByText("Matches exactly")).toBeNull();
	});

	it("still selects it like any other suggestion", async () => {
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
