import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import QuickCheckInWidget from "./QuickCheckInWidget";
import { renderWithProviders } from "../../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const opportunity = (
	id: string,
	titleDe: string,
	checkInMethod: "QRCode" | "PinCode",
) => ({
	id,
	titleDe,
	titleEn: undefined,
	checkInMethod,
	organizationId: ORG_ID,
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	status: "Published",
	isRemote: true,
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	totalMaxParticipants: 5,
	currentParticipantCount: 0,
});

beforeEach(() => {
	api.__reset();
});

function renderWidget() {
	return renderWithProviders(
		<QuickCheckInWidget
			organizationId={ORG_ID}
			refreshKey={0}
			size="full"
			onOpportunityCreated={() => {}}
		/>,
		{ auth: { isAuthenticated: true } },
	);
}

describe("QuickCheckInWidget", () => {
	it("offers only the QR-code opportunity when the org has both kinds", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000001",
					"Scan me",
					"QRCode",
				),
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PinCode",
				),
			],
			pageCount: 1,
			totalCount: 2,
		});

		renderWidget();

		// Opened first: nothing is selected, so the options only exist while the
		// listbox is open and the closed trigger shows a placeholder either way.
		await userEvent.click(await screen.findByRole("combobox"));

		const options = screen.getAllByRole("option");
		expect(options).toHaveLength(1);
		expect(options[0]).toHaveTextContent("Scan me");
		expect(screen.queryByText("Type a PIN")).toBeNull();
		expect(screen.getByTestId("quick-checkin-scan-btn")).toBeEnabled();
	});

	it("shows the empty state when every published opportunity uses another method", async () => {
		api.getOrganizationOpportunities.mockResolvedValue({
			items: [
				opportunity(
					"aaaaaaaa-0000-0000-0000-000000000002",
					"Type a PIN",
					"PinCode",
				),
			],
			pageCount: 1,
			totalCount: 1,
		});

		renderWidget();

		expect(
			await screen.findByText(
				"No published opportunities using QR code check-in yet.",
			),
		).toBeInTheDocument();
		expect(screen.queryByRole("combobox")).toBeNull();
		expect(screen.queryByTestId("quick-checkin-scan-btn")).toBeNull();
	});
});
