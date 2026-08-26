import { describe, it, expect } from "vitest";
import { useState } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Field from "./Field";
import { RequiredFieldsLegend, RequiredMark } from "./RequiredMark";
import TagsInput from "./TagsInput";
import { inputClass, textareaClass } from "../lib/formClasses";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("form field a11y", () => {
	it("has no violations for a form of labelled, required and optional controls", async () => {
		renderWithProviders(
			<form>
				<RequiredFieldsLegend />
				<Field label="Name" id="org-name" required>
					<input id="org-name" className={inputClass} required />
				</Field>
				<Field label="Description" id="org-description">
					<textarea id="org-description" className={textareaClass} />
				</Field>
			</form>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations for a field showing an inline error, and wires it up correctly", async () => {
		renderWithProviders(
			<Field
				label="Name"
				id="org-name"
				required
				error="Enter an organization name."
			>
				<input
					id="org-name"
					className={inputClass}
					required
					aria-invalid="true"
					aria-describedby="org-name-error"
				/>
			</Field>,
		);
		await expectNoA11yViolations();

		const alert = screen.getByRole("alert");
		expect(alert).toHaveAttribute("id", "org-name-error");
		expect(alert).toHaveTextContent("Enter an organization name.");
	});

	it("keeps the required marker out of the control's accessible name", async () => {
		renderWithProviders(
			<Field label="Name" id="org-name" required>
				<input id="org-name" required />
			</Field>,
		);
		const input = screen.getByRole("textbox", { name: "Name" });
		expect(input).toBeRequired();
		expect(input).toHaveAccessibleName("Name");
	});

	it("hides the standalone marker and its legend from the a11y tree", async () => {
		const { container } = renderWithProviders(
			<div>
				<RequiredFieldsLegend />
				<span>
					Name
					<RequiredMark />
				</span>
			</div>,
		);
		for (const marked of container.querySelectorAll("p, span[aria-hidden]")) {
			expect(marked).toHaveAttribute("aria-hidden", "true");
		}
		await expectNoA11yViolations();
	});

	it("has no violations for TagsInput, empty and populated", async () => {
		function Harness() {
			const [tags, setTags] = useState<string[]>([]);
			return (
				<TagsInput
					id="tags"
					label="Tags"
					value={tags}
					onChange={setTags}
					placeholder="Add a tag"
					hint="Press Enter to add a tag."
				/>
			);
		}
		renderWithProviders(<Harness />);
		await expectNoA11yViolations();

		const input = screen.getByRole("textbox", { name: "Tags" });
		await userEvent.type(input, "cleanup{Enter}");
		await userEvent.type(input, "outdoors{Enter}");
		expect(screen.getByRole("list", { name: "Tags" })).toBeInTheDocument();
		await expectNoA11yViolations();
	});
});
