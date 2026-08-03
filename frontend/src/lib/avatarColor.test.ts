import { describe, it, expect } from "vitest";
import { avatarColorClasses } from "./avatarColor";

describe("avatarColorClasses", () => {
	it("is deterministic for the same seed", () => {
		expect(avatarColorClasses("org-1")).toEqual(avatarColorClasses("org-1"));
	});

	it("picks different pairs for different seeds", () => {
		// Not a strict guarantee for arbitrary seeds (a fixed-size palette must
		// collide eventually), but these two are chosen to land on different
		// palette entries, which is the whole point of #993: two organizations
		// sharing an initial should not also share a color.
		expect(
			avatarColorClasses("11111111-1111-1111-1111-111111111111"),
		).not.toEqual(avatarColorClasses("22222222-2222-2222-2222-222222222222"));
	});

	it("always returns a non-empty bg and text class", () => {
		const { bg, text } = avatarColorClasses("some-org-id");
		expect(bg).toMatch(/^bg-/);
		expect(text).toMatch(/^text-/);
	});
});
