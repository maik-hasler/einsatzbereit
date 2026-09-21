import { describe, it, expect } from "vitest";
import { hasRole, readRoles } from "./authRoles";

describe("readRoles", () => {
	it("returns the roles Keycloak sent", () => {
		expect(readRoles(["user", "organisator"])).toEqual(["user", "organisator"]);
	});

	it("returns nothing for a user who carries no roles claim", () => {
		expect(readRoles(undefined)).toEqual([]);
		expect(readRoles(null)).toEqual([]);
	});

	// The claim is typed `unknown` for a reason: a mapper misconfiguration can
	// deliver a string, and a missing one delivers nothing. Neither should
	// throw where a role check happens - which is in a render.
	it("returns nothing for a claim that is not an array", () => {
		expect(readRoles("admin")).toEqual([]);
		expect(readRoles({ 0: "admin" })).toEqual([]);
		expect(readRoles(42)).toEqual([]);
	});

	it("drops non-string entries rather than passing them on", () => {
		expect(readRoles(["user", 7, null, "admin"])).toEqual(["user", "admin"]);
	});

	it("returns an empty list, not a shared one", () => {
		const first = readRoles(undefined);
		first.push("admin");
		expect(readRoles(undefined)).toEqual([]);
	});
});

describe("hasRole", () => {
	it("is true only for a role the claim actually carries", () => {
		expect(hasRole(["organisator"], "organisator")).toBe(true);
		expect(hasRole(["organisator"], "admin")).toBe(false);
	});

	it("is false for every role when there is no claim", () => {
		expect(hasRole(undefined, "admin")).toBe(false);
	});

	// Realm roles are case-sensitive in Keycloak; matching loosely here would
	// grant on a claim the backend would reject.
	it("does not match a differently-cased role", () => {
		expect(hasRole(["Admin"], "admin")).toBe(false);
	});
});
