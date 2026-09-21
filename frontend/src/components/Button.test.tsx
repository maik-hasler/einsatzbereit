import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Button from "./Button";
import { renderWithProviders } from "../test/render";

describe("Button as a plain button", () => {
	it("renders a button element by default", () => {
		renderWithProviders(<Button>Save</Button>);

		expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
	});

	it("calls onClick when pressed", async () => {
		const onClick = vi.fn();
		renderWithProviders(<Button onClick={onClick}>Save</Button>);

		await userEvent.click(screen.getByRole("button", { name: "Save" }));

		expect(onClick).toHaveBeenCalledTimes(1);
	});

	it("does not call onClick while disabled", async () => {
		const onClick = vi.fn();
		renderWithProviders(
			<Button disabled onClick={onClick}>
				Save
			</Button>,
		);

		const button = screen.getByRole("button", { name: "Save" });
		expect(button).toBeDisabled();
		await userEvent.click(button);

		expect(onClick).not.toHaveBeenCalled();
	});

	it("forwards arbitrary button attributes", () => {
		renderWithProviders(
			<Button type="submit" aria-describedby="hint">
				Save
			</Button>,
		);

		const button = screen.getByRole("button", { name: "Save" });
		expect(button).toHaveAttribute("type", "submit");
		expect(button).toHaveAttribute("aria-describedby", "hint");
	});
});

describe("Button as a navigation target", () => {
	it("renders a router link when given `to`", () => {
		renderWithProviders(<Button to="/opportunities">Browse</Button>);

		const link = screen.getByRole("link", { name: "Browse" });
		expect(link).toHaveAttribute("href", "/opportunities");
		expect(link.tagName).toBe("A");
	});

	it("renders a plain anchor when given `href`", () => {
		renderWithProviders(
			<Button href="https://example.test" target="_blank">
				External
			</Button>,
		);

		const link = screen.getByRole("link", { name: "External" });
		expect(link).toHaveAttribute("href", "https://example.test");
		expect(link).toHaveAttribute("target", "_blank");
	});

	// `to` and `href` are both optional in the union, so an explicit
	// `to={undefined}` (a conditional that resolved to nothing) has to fall
	// through to a button rather than render a link with no destination.
	it("falls back to a button when `to` is explicitly undefined", () => {
		renderWithProviders(<Button to={undefined}>Save</Button>);

		expect(screen.getByRole("button", { name: "Save" })).toBeInTheDocument();
		expect(screen.queryByRole("link")).not.toBeInTheDocument();
	});
});

describe("Button styling", () => {
	function classesOf(name: string): string {
		return screen.getByRole("button", { name }).className;
	}

	it("applies the primary variant and medium size by default", () => {
		renderWithProviders(<Button>Save</Button>);

		const classes = classesOf("Save");
		expect(classes).toContain("bg-brand-700");
		// 44px minimum touch target - see the comment on SIZE_CLASSES.
		expect(classes).toContain("min-h-11");
		expect(classes).toContain("rounded-xl");
	});

	it.each([
		["secondary", "text-gray-600"],
		["danger", "bg-red-600"],
		["tertiary", "text-brand-700"],
		["outline", "border-gray-500"],
		["onDark", "text-brand-800"],
	] as const)("applies the %s variant", (variant, expectedClass) => {
		renderWithProviders(<Button variant={variant}>Save</Button>);

		expect(classesOf("Save")).toContain(expectedClass);
	});

	it.each([
		["sm", "text-xs"],
		["lg", "text-base"],
	] as const)("applies the %s size", (size, expectedClass) => {
		renderWithProviders(<Button size={size}>Save</Button>);

		expect(classesOf("Save")).toContain(expectedClass);
	});

	it("rounds fully when pill", () => {
		renderWithProviders(<Button pill>Save</Button>);

		const classes = classesOf("Save");
		expect(classes).toContain("rounded-full");
		expect(classes).not.toContain("rounded-xl");
	});

	it("stretches when fullWidth", () => {
		renderWithProviders(<Button fullWidth>Save</Button>);

		expect(classesOf("Save")).toContain("w-full");
	});

	it("does not stretch by default", () => {
		renderWithProviders(<Button>Save</Button>);

		expect(classesOf("Save")).not.toContain("w-full");
	});

	it("appends a caller's className rather than replacing the variant", () => {
		renderWithProviders(<Button className="mt-4">Save</Button>);

		const classes = classesOf("Save");
		expect(classes).toContain("mt-4");
		expect(classes).toContain("bg-brand-700");
	});

	it("leaves no double spaces when className is omitted", () => {
		renderWithProviders(<Button>Save</Button>);

		expect(classesOf("Save")).not.toMatch(/\s{2,}/);
	});
});
