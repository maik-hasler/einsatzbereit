import { describe, expect, it } from "vitest";
import { screen } from "@testing-library/react";
import BadgeGrid from "./BadgeGrid";
import type { BadgeCatalogEntry } from "../client/api-client";
import { renderWithProviders } from "../test/render";

/**
 * The badge-grid half of `AchievementCopyTests`, moved down from
 * `VisualTests` in #2148 wave 7.
 *
 * These are locale-file values: `BadgeCard` resolves every name and
 * description through `t("achievements.badges.<key>.<field>")`, so the
 * rendered German copy is a pure function of `de.json` plus the catalog key.
 * The E2E version signed vera in, seeded a login streak through the API to
 * dodge a `LoginStreakMiddleware` dedup race, loaded /profile and switched
 * the language through the header menu - all of it setup to reach copy that
 * is a prop and a locale file here.
 *
 * Regressions covered:
 * - The 100-confirmations badge was "Hundertschaft", in German first a
 *   police/riot-unit term and a jarring association for a civic volunteering
 *   product. Renamed to the plain "100 Einsaetze".
 * - (#1788) "Auf Kurs" was described as a "Login-Serie" - Denglish that hid
 *   the rule behind loan vocabulary.
 * - (#1848) Its replacement "Aktive Woche" was its own regression: it reads
 *   as a *weekly* concept, confusable with the "Wochenheld" (weekly-hero-4)
 *   badge on the same grid, which is a genuinely different
 *   4-consecutive-weeks metric. Renamed to "Anmeldeserie", using the app's
 *   own "anmelden" login terminology, with a description naming the unit it
 *   measures - days, not weeks.
 */

// The real catalog keys, because the copy is keyed on them. Names and
// descriptions here are the API's own English strings and are deliberately
// ignored by BadgeCard - if a regression ever made it render these instead of
// the translation, every assertion below would catch it.
const catalog: BadgeCatalogEntry[] = [
	{
		key: "centurion-100",
		type: 0,
		name: "100 engagements",
		description: "Earned after 100 confirmed engagements.",
		isHidden: false,
	},
	{
		key: "on-a-roll-7",
		type: 2,
		name: "On a roll",
		description: "Earned for 7 consecutive days with a sign-in.",
		isHidden: false,
	},
	{
		key: "weekly-hero-4",
		type: 2,
		name: "Weekly hero",
		description: "Earned for 4 consecutive active weeks.",
		isHidden: false,
	},
];

describe("BadgeGrid German copy", () => {
	// BadgeGrid renders every catalog entry, earned or not (only isHidden ones
	// are masked, and none of these is), so no achievement history is needed.
	const renderGrid = () =>
		renderWithProviders(<BadgeGrid earned={[]} catalog={catalog} />, {
			lng: "de",
		});

	it("names the 100-confirmations badge factually, not 'Hundertschaft'", () => {
		const { container } = renderGrid();

		expect(
			container.querySelector("#badge-name-centurion-100"),
		).toHaveTextContent("100 Einsätze");
		// Nowhere in the DOM, not merely invisible - the tooltip copy counts too.
		expect(screen.queryByText(/Hundertschaft/)).toBeNull();
	});

	it("names the login-streak badge 'Anmeldeserie' and states the rule in days", () => {
		const { container } = renderGrid();

		expect(
			container.querySelector("#badge-name-on-a-roll-7"),
		).toHaveTextContent("Anmeldeserie");
		expect(
			container.querySelector("#badge-tooltip-on-a-roll-7"),
		).toHaveTextContent(
			"Verdient für 7 aufeinanderfolgende Tage mit Anmeldung.",
		);
	});

	it("uses neither the Denglish 'Login-Serie' nor the week-confusable 'Aktive Woche'", () => {
		const { container } = renderGrid();

		expect(screen.queryByText(/Login-Serie/)).toBeNull();
		// #1848: must not read as a weekly concept confusable with the adjacent
		// "Wochenheld" badge, which is on this same grid - asserted present so
		// the pair is shown to be distinguishable, not merely one of them absent.
		expect(screen.queryByText(/Aktive Woche/)).toBeNull();
		expect(
			container.querySelector("#badge-name-weekly-hero-4"),
		).toHaveTextContent("Wochenheld");
	});
});
