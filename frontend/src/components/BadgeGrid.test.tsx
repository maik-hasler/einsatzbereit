import { describe, expect, it } from "vitest";
import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import BadgeGrid from "./BadgeGrid";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import { renderWithProviders } from "../test/render";

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

const earnedOnlyCatalog: BadgeCatalogEntry[] = [
	{
		key: "trailblazer",
		type: 0,
		name: "Trailblazer",
		description: "Earned after your very first opportunity.",
		isHidden: false,
	},
];

const earnedFirst: AchievementSummary[] = [
	{
		id: "ach-1",
		type: "FirstEngagement",
		key: "trailblazer",
		name: "Trailblazer",
		description: "Earned after your very first opportunity.",
		unlockedAt: new Date(Date.UTC(2026, 6, 1, 10, 0)),
	},
];

describe("BadgeGrid tooltip", () => {
	it("shows the description inline on an earned badge, not only in the tooltip", () => {
		renderWithProviders(
			<BadgeGrid earned={earnedFirst} catalog={earnedOnlyCatalog} />,
		);

		const card = screen.getByRole("group", { name: "Trailblazer" });
		expect(card).toHaveTextContent(
			"Earned after your very first opportunity.",
		);
	});

	it("dismisses the tooltip with Escape without moving focus away", () => {
		renderWithProviders(
			<BadgeGrid earned={earnedFirst} catalog={earnedOnlyCatalog} />,
		);

		const card = screen.getByRole("group", { name: "Trailblazer" });
		const tooltip = screen.getByRole("tooltip", { hidden: true });

		card.focus();
		fireEvent.focus(card);
		expect(tooltip).toHaveClass("block");

		fireEvent.keyDown(card, { key: "Escape" });

		expect(tooltip).toHaveClass("hidden");
		expect(card).toHaveFocus();
	});

	it("stays open when the pointer moves from the card onto the tooltip itself", async () => {
		renderWithProviders(
			<BadgeGrid earned={earnedFirst} catalog={earnedOnlyCatalog} />,
		);

		const card = screen.getByRole("group", { name: "Trailblazer" });
		const tooltip = screen.getByRole("tooltip", { hidden: true });

		await userEvent.hover(card);
		expect(tooltip).toHaveClass("block");

		await userEvent.unhover(card);
		await userEvent.hover(tooltip);
		expect(tooltip).toHaveClass("block");
	});
});

describe("BadgeGrid German copy", () => {
	const renderGrid = () =>
		renderWithProviders(<BadgeGrid earned={[]} catalog={catalog} />, {
			lng: "de",
		});

	it("names the 100-confirmations badge factually, not 'Hundertschaft'", () => {
		const { container } = renderGrid();

		expect(
			container.querySelector("#badge-name-centurion-100"),
		).toHaveTextContent("100 Einsätze");
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
		expect(screen.queryByText(/Aktive Woche/)).toBeNull();
		expect(
			container.querySelector("#badge-name-weekly-hero-4"),
		).toHaveTextContent("Wochenheld");
	});
});
