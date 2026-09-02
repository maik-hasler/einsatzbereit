import { describe, it } from "vitest";
import OpportunityActionPanel, {
	type PanelFact,
} from "./OpportunityActionPanel";
import Button from "./Button";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";
import { CalendarIcon, MapPinIcon, UserGroupIcon } from "./icons";

const facts: PanelFact[] = [
	{
		key: "when",
		icon: <CalendarIcon className="h-4 w-4" />,
		label: "When",
		value: "27.08.2026, 09:00",
	},
	{
		key: "how",
		icon: <UserGroupIcon className="h-4 w-4" />,
		label: "How it works",
		value: "2 time slots",
	},
	{
		key: "where",
		icon: <MapPinIcon className="h-4 w-4" />,
		label: "Where",
		value: "Kiel",
	},
];

describe("OpportunityActionPanel a11y", () => {
	it("has no violations with a status note and a call to action", async () => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{
					label: "Only 2 spots left!",
					tone: "urgent",
					note: "4 people have already joined",
				}}
				facts={facts}
			>
				<Button fullWidth size="lg">
					Sign up for a slot
				</Button>
			</OpportunityActionPanel>,
		);

		await expectNoA11yViolations();
	});

	it("has no violations as the bare facts card an owner's draft renders", async () => {
		renderWithProviders(
			<OpportunityActionPanel
				status={{ label: "No open spots", tone: "closed" }}
				facts={facts}
			/>,
		);

		await expectNoA11yViolations();
	});
});
