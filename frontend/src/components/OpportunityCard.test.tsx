import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import OpportunityCard, { type OpportunityCardItem } from "./OpportunityCard";
import { renderWithProviders } from "../test/render";

const base: OpportunityCardItem = {
	id: "11111111-1111-1111-1111-111111111111",
	titleDe: "Deutscher Titel",
	titleEn: "English Title",
	descriptionDe: "Beschreibung.",
	descriptionEn: "Description.",
	street: "Teststrasse",
	houseNumber: "1",
	zipCode: "24103",
	city: "Kiel",
	isRemote: false,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	category: "Environment",
	totalMaxParticipants: 10,
	currentParticipantCount: 2,
	validUntil: undefined,
	nextTimeSlotStart: new Date(Date.UTC(2027, 0, 14, 9, 0)),
	organizationId: "22222222-2222-2222-2222-222222222222",
	organizationName: "Freiwillige Feuerwehr Kiel",
};

const renderCard = (item: OpportunityCardItem, keyword?: string) =>
	renderWithProviders(
		<OpportunityCard item={item} headingLevel={3} keyword={keyword} />,
	);

describe("OpportunityCard date and capacity contract", () => {
	it.each([
		[
			"a scheduled opportunity",
			{ ...base, nextTimeSlotStart: new Date(Date.UTC(2027, 0, 14, 9, 0)) },
			"start",
		],
		[
			"an interest-based one with a deadline",
			{
				...base,
				participationType: "IndividualContact",
				nextTimeSlotStart: undefined,
				validUntil: new Date(Date.UTC(2027, 0, 31)),
			},
			"deadline",
		],
		[
			"one with neither",
			{
				...base,
				participationType: "IndividualContact",
				nextTimeSlotStart: undefined,
				validUntil: undefined,
			},
			"flexible",
		],
	])("states a date kind and a capacity for %s", (_label, item, kind) => {
		renderCard(item as OpportunityCardItem);

		expect(screen.getByTestId("opportunity-date-line")).toHaveAttribute(
			"data-date-kind",
			kind,
		);
		expect(
			screen.getByTestId("opportunity-capacity").textContent?.trim(),
		).not.toBe("");
	});

	it("omits the capacity chip entirely for an interest-based opportunity, which has no capacity to count (#2228)", () => {
		renderCard({
			...base,
			participationType: "IndividualContact",
			nextTimeSlotStart: undefined,
			totalMaxParticipants: 0,
			currentParticipantCount: 0,
		});

		expect(screen.queryByTestId("opportunity-capacity")).toBeNull();
	});
});

describe("OpportunityCard sign-up mechanism chip (#2228)", () => {
	it("states how to sign up for a scheduled opportunity", () => {
		renderCard(base);

		expect(
			screen.getByTestId("opportunity-signup-mechanism"),
		).toHaveTextContent("Scheduled slots");
	});

	it("states the participation type for an interest-based one too, so the position always means the same thing", () => {
		renderCard({
			...base,

			participationType: "IndividualContact",
			nextTimeSlotStart: undefined,
			validUntil: new Date(Date.UTC(2027, 0, 31)),
			totalMaxParticipants: 0,
			currentParticipantCount: 0,
		});

		expect(
			screen.getByTestId("opportunity-signup-mechanism"),
		).toHaveTextContent("By expression of interest");
		expect(screen.queryByTestId("opportunity-capacity")).toBeNull();
	});
});

describe("OpportunityCard organization badge", () => {
	it("shows the uploaded logo instead of initials", () => {
		renderCard({
			...base,
			organizationLogoUrl: "https://storage.example.test/logos/kiel.png",
		});

		const link = screen.getByTestId("opportunity-org-link");
		const img = link.querySelector("img");
		expect(img).not.toBeNull();
		expect(img).toHaveAttribute(
			"src",
			"https://storage.example.test/logos/kiel.png",
		);
		expect(link.querySelector("[aria-hidden='true']")).toBeNull();
	});

	it("falls back to initials when there is no logo", () => {
		renderCard(base);

		const link = screen.getByTestId("opportunity-org-link");
		expect(link.querySelector("img")).toBeNull();
		expect(link.querySelector("[aria-hidden='true']")).toHaveTextContent("FK");
	});
});

describe("OpportunityCard cross-locale search match notice (#2242)", () => {
	it("explains a match that only exists in the hidden German title", () => {
		renderCard(base, "Deutscher");

		expect(
			screen.getByTestId("opportunity-cross-locale-match"),
		).toHaveTextContent("Deutscher Titel");
	});

	it("stays silent when the keyword already appears in the displayed English title", () => {
		renderCard(base, "English");

		expect(screen.queryByTestId("opportunity-cross-locale-match")).toBeNull();
	});

	it("stays silent when no keyword is active", () => {
		renderCard(base);

		expect(screen.queryByTestId("opportunity-cross-locale-match")).toBeNull();
	});

	it("stays silent when the keyword only matches the already-visible organization name", () => {
		renderCard(base, "Feuerwehr");

		expect(screen.queryByTestId("opportunity-cross-locale-match")).toBeNull();
	});
});
