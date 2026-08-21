import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import OpportunityCard, { type OpportunityCardItem } from "./OpportunityCard";
import { renderWithProviders } from "../test/render";

/**
 * `OpportunityCardContractTests`' card-shape cases and
 * `AvatarAndLogoDisplayTests`' logo case, moved down in #2148 wave 13.
 * Remaining inventory: #2159.
 *
 * Since #2054 there is exactly one opportunity card, shared by every surface
 * that shows one - which is what makes these a component contract rather than
 * a per-page one. #1943's own defect was two components resolving the same
 * badge through different i18n keys; with one component and one key, asserting
 * it once here is the whole guarantee, where the E2E had to visit two pages to
 * compare them.
 *
 * That the *DTOs* carry the fields these render is a separate claim, and a
 * real one - a card cannot tell "the field never arrived" from "this branch
 * chose not to render". It is asserted in
 * `IntegrationTests/GetPublicOrganizationProfileTests.cs`, also as part of
 * #2148.
 */
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

const renderCard = (item: OpportunityCardItem) =>
	renderWithProviders(<OpportunityCard item={item} headingLevel={3} />);

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
		// #1777: the line under the title was either a start date or an
		// application deadline, rendered with identical classes and the same
		// calendar icon, so only its label said which - and the capacity chip
		// appeared on some cards and not others.
		renderCard(item as OpportunityCardItem);

		expect(screen.getByTestId("opportunity-date-line")).toHaveAttribute(
			"data-date-kind",
			kind,
		);
		expect(
			screen.getByTestId("opportunity-capacity").textContent?.trim(),
		).not.toBe("");
	});

	it("still states a capacity when there are no places to count", () => {
		// The state that used to render nothing at all, which made the chip's
		// presence look like a property of the opportunity rather than of the
		// data.
		renderCard({
			...base,
			participationType: "IndividualContact",
			nextTimeSlotStart: undefined,
			totalMaxParticipants: 0,
			currentParticipantCount: 0,
		});

		expect(
			screen.getByTestId("opportunity-capacity").textContent?.trim(),
		).not.toBe("");
	});
});

describe("OpportunityCard sign-up mechanism chip", () => {
	it("states how to sign up for a scheduled opportunity", () => {
		renderCard(base);

		expect(
			screen.getByTestId("opportunity-signup-mechanism"),
		).toHaveTextContent("Scheduled slots");
	});

	it("omits the chip for an interest-based one, whose capacity already says it", () => {
		// `showSignUpMechanismChip` is `participationType === "ScheduledSlots"`.
		// An interest-based card's capacity chip already reads "By expression of
		// interest", so a second chip would state the same fact twice.
		// `totalMaxParticipants: 0` is the tri-state's "no time slots" value (see
		// lib/opportunityCapacity.ts), which for an IndividualContact
		// opportunity is what resolves the chip to "By expression of interest"
		// rather than to a spots-left count.
		renderCard({
			...base,
			participationType: "IndividualContact",
			nextTimeSlotStart: undefined,
			validUntil: new Date(Date.UTC(2027, 0, 31)),
			totalMaxParticipants: 0,
			currentParticipantCount: 0,
		});

		expect(screen.queryByTestId("opportunity-signup-mechanism")).toBeNull();
		expect(screen.getByTestId("opportunity-capacity")).toHaveTextContent(
			"By expression of interest",
		);
	});
});

describe("OpportunityCard organization badge", () => {
	it("shows the uploaded logo instead of initials", () => {
		// #588: the badge always drew initials, even with an image on file,
		// because it never read the field that already carried the URL.
		renderCard({
			...base,
			organizationLogoUrl: "https://storage.example.test/logos/kiel.png",
		});

		// By tag, not by role: the logo carries alt="" because the link it sits
		// in is already named by the organization, so it is presentational.
		const link = screen.getByTestId("opportunity-org-link");
		const img = link.querySelector("img");
		expect(img).not.toBeNull();
		expect(img).toHaveAttribute(
			"src",
			"https://storage.example.test/logos/kiel.png",
		);
		// And the initials fallback is gone, rather than sitting behind it.
		expect(link.querySelector("[aria-hidden='true']")).toBeNull();
	});

	it("falls back to initials when there is no logo", () => {
		renderCard(base);

		const link = screen.getByTestId("opportunity-org-link");
		expect(link.querySelector("img")).toBeNull();
		// "FK" - first and last word of "Freiwillige Feuerwehr Kiel", see
		// lib/initials.ts.
		expect(link.querySelector("[aria-hidden='true']")).toHaveTextContent("FK");
	});
});
