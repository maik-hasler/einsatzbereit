import { describe, it, expect } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import TagsInput from "./TagsInput";
import { renderWithProviders } from "../test/render";

/**
 * Driven through a controlled wrapper rather than a bare `value`/`onChange`
 * pair, because most of what this component decides - rejecting a duplicate,
 * removing the last tag on Backspace - only means anything against the list it
 * just changed.
 */
function Harness({ initial = [] as string[] }) {
	const [tags, setTags] = useState(initial);
	return (
		<TagsInput
			id="tags"
			label="Tags"
			value={tags}
			onChange={setTags}
			placeholder="Add a tag"
			hint="Press Enter to add"
		/>
	);
}

function renderTags(initial: string[] = []) {
	renderWithProviders(<Harness initial={initial} />);
	return screen.getByLabelText("Tags", { selector: "input" });
}

describe("TagsInput adding", () => {
	it("commits the draft on Enter", async () => {
		const input = renderTags();

		await userEvent.type(input, "gardening{Enter}");

		expect(screen.getByText("gardening")).toBeInTheDocument();
		expect(input).toHaveValue("");
	});

	// A comma is what someone typing a list reaches for before they think of
	// Enter, and it must never end up inside a tag.
	it("commits the draft on a comma", async () => {
		const input = renderTags();

		await userEvent.type(input, "gardening,");

		expect(screen.getByText("gardening")).toBeInTheDocument();
		expect(input).toHaveValue("");
	});

	// Someone who types a tag and tabs away meant to add it.
	it("commits the draft on blur", async () => {
		const input = renderTags();

		await userEvent.type(input, "gardening");
		await userEvent.tab();

		expect(screen.getByText("gardening")).toBeInTheDocument();
	});

	it("ignores whitespace-only input", async () => {
		const input = renderTags();

		await userEvent.type(input, "   {Enter}");

		expect(screen.getByRole("status")).toHaveTextContent("");
		expect(input).toHaveValue("");
	});

	it("trims the tag it stores", async () => {
		const input = renderTags();

		await userEvent.type(input, "  gardening  {Enter}");

		expect(screen.getByText("gardening")).toBeInTheDocument();
	});

	it("refuses a duplicate regardless of case, and says so", async () => {
		const input = renderTags(["Gardening"]);

		await userEvent.type(input, "gardening{Enter}");

		expect(screen.getByRole("status")).toHaveTextContent(
			"gardening already added.",
		);
		expect(
			within(screen.getByRole("list", { name: "Tags" })).getAllByRole(
				"listitem",
			),
		).toHaveLength(1);
	});

	// `maxLength` on the field already stops a person reaching 51 characters, so
	// the value is set directly here - the guard exists for the day that
	// attribute is dropped or a paste path sidesteps it, and an untested guard
	// is one nobody knows is broken.
	it("refuses a tag past the length limit, and says so", async () => {
		const input = renderTags();

		fireEvent.change(input, { target: { value: "x".repeat(51) } });
		await userEvent.type(input, "{Enter}");

		expect(screen.getByRole("status")).toHaveTextContent(
			"A tag must not exceed 50 characters.",
		);
	});

	it("refuses a tag past the count limit, and says so", async () => {
		const input = renderTags(
			Array.from({ length: 20 }, (_, i) => `tag-${i + 1}`),
		);

		await userEvent.type(input, "one-too-many{Enter}");

		expect(screen.getByRole("status")).toHaveTextContent(
			"You can add up to 20 tags.",
		);
		expect(screen.queryByText("one-too-many")).not.toBeInTheDocument();
	});
});

describe("TagsInput removing", () => {
	it("removes a tag from its own remove button", async () => {
		renderTags(["gardening", "cleanup"]);

		await userEvent.click(
			screen.getByRole("button", { name: "Remove tag gardening" }),
		);

		expect(screen.queryByText("gardening")).not.toBeInTheDocument();
		expect(screen.getByText("cleanup")).toBeInTheDocument();
	});

	// The remove button disappears with the tag it belonged to, so focus would
	// otherwise fall back to the body and a keyboard user would lose their place.
	it("returns focus to the field after removing", async () => {
		const input = renderTags(["gardening"]);

		await userEvent.click(
			screen.getByRole("button", { name: "Remove tag gardening" }),
		);

		expect(input).toHaveFocus();
	});

	it("removes the last tag on Backspace in an empty field", async () => {
		const input = renderTags(["gardening", "cleanup"]);

		await userEvent.click(input);
		await userEvent.keyboard("{Backspace}");

		expect(screen.getByText("gardening")).toBeInTheDocument();
		expect(screen.queryByText("cleanup")).not.toBeInTheDocument();
	});

	it("leaves the tags alone when Backspace edits the draft", async () => {
		const input = renderTags(["gardening"]);

		await userEvent.type(input, "ab{Backspace}");

		expect(screen.getByText("gardening")).toBeInTheDocument();
		expect(input).toHaveValue("a");
	});
});

describe("TagsInput announcements and labelling", () => {
	it("announces an addition and a removal politely", async () => {
		const input = renderTags();

		await userEvent.type(input, "gardening{Enter}");
		expect(screen.getByRole("status")).toHaveTextContent("gardening added.");

		await userEvent.click(
			screen.getByRole("button", { name: "Remove tag gardening" }),
		);
		expect(screen.getByRole("status")).toHaveTextContent("gardening removed.");
	});

	it("names the tag list for a screen reader", () => {
		renderTags(["gardening"]);

		expect(screen.getByRole("list", { name: "Tags" })).toBeInTheDocument();
	});

	it("ties the hint to the field", () => {
		const input = renderTags();

		expect(input).toHaveAccessibleDescription("Press Enter to add");
	});

	// A placeholder next to existing tags reads as another tag.
	it("shows the placeholder only while the list is empty", async () => {
		const input = renderTags();
		expect(input).toHaveAttribute("placeholder", "Add a tag");

		await userEvent.type(input, "gardening{Enter}");

		expect(input).not.toHaveAttribute("placeholder");
	});
});
