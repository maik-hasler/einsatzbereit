import { describe, expect, it } from "vitest";
import { sortByLabelPrefixMatch } from "./citySuggestionSort";

describe("sortByLabelPrefixMatch", () => {
	// The bug this closes (#1856): "Leip" returned ["Leip", "Lindenwalde"]
	// with no "Leipzig", because the upstream geocoder's own order buried a
	// true prefix match behind an unrelated substring match.
	it("sorts a prefix match ahead of a match that only contains the query elsewhere", () => {
		const results = [{ label: "Lindenwalde" }, { label: "Leipzig" }];

		expect(sortByLabelPrefixMatch(results, "Leip")).toEqual([
			{ label: "Leipzig" },
			{ label: "Lindenwalde" },
		]);
	});

	it("matches case-insensitively", () => {
		const results = [{ label: "leipzig" }];

		expect(sortByLabelPrefixMatch(results, "LEIP")).toEqual([
			{ label: "leipzig" },
		]);
	});

	it("keeps the upstream order stable within each group", () => {
		const results = [
			{ label: "Berlin-Mitte" },
			{ label: "Berlin-Spandau" },
			{ label: "Neuberlin" },
		];

		expect(sortByLabelPrefixMatch(results, "Berlin")).toEqual([
			{ label: "Berlin-Mitte" },
			{ label: "Berlin-Spandau" },
			{ label: "Neuberlin" },
		]);
	});

	it("ignores leading/trailing whitespace on the query", () => {
		const results = [{ label: "Dresden" }, { label: "Weißdresden" }];

		expect(sortByLabelPrefixMatch(results, "  dresden  ")).toEqual([
			{ label: "Dresden" },
			{ label: "Weißdresden" },
		]);
	});

	it("does not mutate the input array", () => {
		const results = [{ label: "Lindenwalde" }, { label: "Leipzig" }];

		sortByLabelPrefixMatch(results, "Leip");

		expect(results).toEqual([{ label: "Lindenwalde" }, { label: "Leipzig" }]);
	});

	it("returns an empty array unchanged", () => {
		expect(sortByLabelPrefixMatch([], "Leip")).toEqual([]);
	});
});
