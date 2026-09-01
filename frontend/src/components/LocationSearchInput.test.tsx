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

function Harness({
	onSelect,
	initialValue = "",
}: {
	onSelect: (s: unknown) => void;
	initialValue?: string;
}) {
	const [value, setValue] = useState(initialValue);
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

	it("asserts no match once the query is long enough to be confident - but only after searching", async () => {
		api.searchCities.mockResolvedValue([]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Xyz");

		// The third character used to flip the helper line straight to "no match", a
		// full debounce interval before the request was even sent - the field claimed
		// the city did not exist before it had looked for it (#2319).
		expect(await screen.findByRole("status")).not.toHaveTextContent(
			"No matching location found.",
		);

		await vi.waitFor(() =>
			expect(screen.getByRole("status")).toHaveTextContent(
				"No matching location found.",
			),
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

describe("LocationSearchInput with an already-committed value", () => {
	it("does not open the suggestion list until the user asks for it", async () => {
		api.searchCities.mockResolvedValue([place("Leipzig")]);

		renderWithProviders(<Harness onSelect={() => {}} initialValue="Leipzig" />);

		// Mounting pre-filled is how reopening the location filter, or loading a
		// shared ?city= URL, arrives here. The lookup still runs - it just must not
		// pop a list over whatever sits beneath this field (#2319).
		await vi.waitFor(() => expect(api.searchCities).toHaveBeenCalled());
		expect(screen.queryByRole("listbox")).toBeNull();

		await userEvent.type(screen.getByLabelText("City"), "z");
		expect(await screen.findByRole("listbox")).toBeInTheDocument();
	});

	// A different city than the case above: useCitySuggestions caches by query at
	// module scope, so reusing "Leipzig" here would serve a cache hit and never
	// exercise the lookup.
	it("reopens on focus, so the list is still one click away", async () => {
		api.searchCities.mockResolvedValue([place("Bremen")]);

		renderWithProviders(<Harness onSelect={() => {}} initialValue="Bremen" />);

		await vi.waitFor(() => expect(api.searchCities).toHaveBeenCalled());
		expect(screen.queryByRole("listbox")).toBeNull();

		await userEvent.click(screen.getByLabelText("City"));
		expect(await screen.findByRole("listbox")).toBeInTheDocument();
	});
});

describe("LocationSearchInput status message placement", () => {
	it("floats the message instead of stretching the field's container", async () => {
		api.searchCities.mockResolvedValue([]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Xyzzy");

		// jsdom has no layout engine, so the guard is the positioning class itself:
		// in the flow this line grew the rounded-full pill the landing page wraps
		// this input in into a two-row blob.
		await vi.waitFor(() =>
			expect(screen.getByRole("status")).toHaveClass("absolute"),
		);
	});

	it("keeps the message audible but hidden once the field is closed", async () => {
		api.searchCities.mockResolvedValue([]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "Xyzzy");
		await vi.waitFor(() =>
			expect(screen.getByRole("status")).toHaveTextContent(
				"No matching location found.",
			),
		);

		await userEvent.tab();

		// It closes with the suggestion list rather than leaving a card hanging
		// under a field nobody is using - but stays mounted as a live region.
		await vi.waitFor(() =>
			expect(screen.getByRole("status")).toHaveClass("sr-only"),
		);
	});
});

describe("LocationSearchInput postal codes", () => {
	it("offers what the server returned for a postal code, however it is labelled", async () => {
		api.searchCities.mockResolvedValue([place("26129 Oldenburg")]);

		renderWithProviders(<Harness onSelect={() => {}} />);

		await userEvent.type(screen.getByLabelText("City"), "26129");

		// The label cannot contain the typed digits, so the client must not try to
		// re-check the server's match - that dropped every postal-code hit.
		const options = await screen.findAllByRole("option");
		expect(options).toHaveLength(1);
		expect(options[0]).toHaveTextContent("26129 Oldenburg");
	});
});
