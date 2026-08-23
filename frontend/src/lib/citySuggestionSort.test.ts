import { describe, expect, it } from "vitest";
import {
	filterByLabelMatch,
	sortByLabelPrefixMatch,
} from "./citySuggestionSort";

describe("filterByLabelMatch", () => {
	it("drops results whose label doesn't contain the query anywhere", () => {
		const results = [
			{ label: "Koeln" },
			{ label: "Leipzig" },
			{ label: "Dresden" },
		];

		expect(filterByLabelMatch(results, "Lei")).toEqual([{ label: "Leipzig" }]);
	});

	it("keeps a label that contains the query anywhere, not just as a prefix", () => {
		const results = [{ label: "Lindenwalde" }, { label: "Koeln" }];

		expect(filterByLabelMatch(results, "wald")).toEqual([
			{ label: "Lindenwalde" },
		]);
	});

	it("matches case-insensitively", () => {
		const results = [{ label: "leipzig" }];

		expect(filterByLabelMatch(results, "LEIP")).toEqual([{ label: "leipzig" }]);
	});

	it("ignores leading/trailing whitespace on the query", () => {
		const results = [{ label: "Dresden" }];

		expect(filterByLabelMatch(results, "  dresden  ")).toEqual([
			{ label: "Dresden" },
		]);
	});

	it("does not mutate the input array", () => {
		const results = [{ label: "Koeln" }, { label: "Leipzig" }];

		filterByLabelMatch(results, "Lei");

		expect(results).toEqual([{ label: "Koeln" }, { label: "Leipzig" }]);
	});

	it("returns an empty array unchanged", () => {
		expect(filterByLabelMatch([], "Lei")).toEqual([]);
	});
});

describe("sortByLabelPrefixMatch", () => {
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
