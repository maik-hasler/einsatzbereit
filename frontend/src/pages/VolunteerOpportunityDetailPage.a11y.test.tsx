import { describe, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Route, Routes } from "react-router";
import VolunteerOpportunityDetailPage from "./VolunteerOpportunityDetailPage";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const OPPORTUNITY_ID = "11111111-1111-1111-1111-111111111111";

function renderDetail() {
	return renderWithProviders(
		<Routes>
			<Route
				path="/volunteer-opportunities/:opportunityId"
				element={<VolunteerOpportunityDetailPage />}
			/>
		</Routes>,
		{ route: `/volunteer-opportunities/${OPPORTUNITY_ID}` },
	);
}

beforeEach(() => {
	api.__reset();
	api.getVolunteerOpportunities.mockResolvedValue({
		items: [],
		pageCount: 1,
		totalCount: 0,
	});
});

describe("VolunteerOpportunityDetailPage a11y", () => {
	it("has no violations when coordinates are missing and the address fallback renders", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			id: OPPORTUNITY_ID,
			organizationId: "22222222-2222-2222-2222-222222222222",
			organizationName: "Bilingual Org",
			titleDe: "Deutscher Titel",
			titleEn: "English Title",
			descriptionDe: "Deutsche Beschreibung.",
			descriptionEn: "English description.",
			street: "Teststrasse",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
			isRemote: false,
			latitude: undefined,
			longitude: undefined,
			occurrence: "OneTime",
			participationType: "IndividualContact",
			checkInMethod: "None",
			status: "Published",
			timeSlots: [],
			tags: [],
			currentUserEngagements: [],
			validUntil: undefined,
			createdOn: new Date(Date.UTC(2026, 7, 1, 9, 0)),
		});

		renderDetail();

		await screen.findByTestId("opportunity-location-fallback");
		await expectNoA11yViolations();
	});
});
