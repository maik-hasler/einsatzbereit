import { describe, it, expect } from "vitest";
import {
	CANONICAL_TIME_ZONE,
	toZonedDatetimeLocalValue,
	zonedDatetimeLocalToUtc,
} from "./timezone";

describe("zonedDatetimeLocalToUtc", () => {
	it("interprets a datetime-local value as Berlin wall-clock time in summer (CEST, UTC+2)", () => {
		expect(
			zonedDatetimeLocalToUtc("2026-08-27T09:00", CANONICAL_TIME_ZONE),
		).toEqual(new Date("2026-08-27T07:00:00.000Z"));
	});

	it("interprets a datetime-local value as Berlin wall-clock time in winter (CET, UTC+1)", () => {
		expect(
			zonedDatetimeLocalToUtc("2026-01-15T09:00", CANONICAL_TIME_ZONE),
		).toEqual(new Date("2026-01-15T08:00:00.000Z"));
	});

	it("resolves the post-transition CEST offset for a slot just after Germany's 2027 spring-forward", () => {
		// Germany's 2027 DST starts 2027-03-28 (clocks 02:00 -> 03:00 CEST).
		expect(
			zonedDatetimeLocalToUtc("2027-03-31T00:30", CANONICAL_TIME_ZONE),
		).toEqual(new Date("2027-03-30T22:30:00.000Z"));
	});

	it("resolves the post-transition CET offset for a slot just before Germany's 2026 fall-back ends", () => {
		// Germany's 2026 DST ends 2026-10-25 (clocks 03:00 -> 02:00 CET).
		expect(
			zonedDatetimeLocalToUtc("2026-10-31T23:30", CANONICAL_TIME_ZONE),
		).toEqual(new Date("2026-10-31T22:30:00.000Z"));
	});

	it("works for a non-Berlin, non-DST zone", () => {
		expect(zonedDatetimeLocalToUtc("2026-06-01T09:00", "Asia/Kolkata")).toEqual(
			new Date("2026-06-01T03:30:00.000Z"),
		);
	});
});

describe("toZonedDatetimeLocalValue", () => {
	it("renders a UTC instant as Berlin wall-clock time in summer (CEST, UTC+2)", () => {
		expect(
			toZonedDatetimeLocalValue(
				new Date("2026-08-27T07:00:00.000Z"),
				CANONICAL_TIME_ZONE,
			),
		).toBe("2026-08-27T09:00");
	});

	it("renders a UTC instant as Berlin wall-clock time in winter (CET, UTC+1)", () => {
		expect(
			toZonedDatetimeLocalValue(
				new Date("2026-01-15T08:00:00.000Z"),
				CANONICAL_TIME_ZONE,
			),
		).toBe("2026-01-15T09:00");
	});

	it("round-trips through zonedDatetimeLocalToUtc", () => {
		const original = "2026-08-27T09:00";
		const utc = zonedDatetimeLocalToUtc(original, CANONICAL_TIME_ZONE);
		expect(toZonedDatetimeLocalValue(utc, CANONICAL_TIME_ZONE)).toBe(original);
	});
});
