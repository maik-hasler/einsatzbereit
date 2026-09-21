import { describe, it, expect, vi } from "vitest";
import { createEventBus } from "./createEventBus";

describe("createEventBus", () => {
	it("does not throw when publishing with no listeners", () => {
		const bus = createEventBus<string>();
		expect(() => bus.publish("hello")).not.toThrow();
	});

	it("hands the published payload to every listener", () => {
		const bus = createEventBus<{ value: number }>();
		const first = vi.fn();
		const second = vi.fn();
		bus.subscribe(first);
		bus.subscribe(second);

		bus.publish({ value: 42 });

		expect(first).toHaveBeenCalledExactlyOnceWith({ value: 42 });
		expect(second).toHaveBeenCalledExactlyOnceWith({ value: 42 });
	});

	it("stops calling a listener after it unsubscribes", () => {
		const bus = createEventBus<string>();
		const listener = vi.fn();
		const unsubscribe = bus.subscribe(listener);

		unsubscribe();
		bus.publish("dropped");

		expect(listener).not.toHaveBeenCalled();
	});

	it("leaves the other listeners subscribed when one unsubscribes", () => {
		const bus = createEventBus<string>();
		const kept = vi.fn();
		const dropped = vi.fn();
		bus.subscribe(kept);
		const unsubscribe = bus.subscribe(dropped);

		unsubscribe();
		bus.publish("event");

		expect(kept).toHaveBeenCalledTimes(1);
		expect(dropped).not.toHaveBeenCalled();
	});

	it("is safe to unsubscribe twice", () => {
		const bus = createEventBus<string>();
		const listener = vi.fn();
		const unsubscribe = bus.subscribe(listener);

		unsubscribe();
		expect(() => unsubscribe()).not.toThrow();
		bus.publish("event");

		expect(listener).not.toHaveBeenCalled();
	});

	// The array-based buses this factory replaced called `listeners.forEach`
	// directly over the live array: a listener that unsubscribed while being
	// called shifted every later entry down one index and the next listener in
	// line was silently skipped. Publishing over a snapshot is what fixes it.
	it("still reaches later listeners when an earlier one unsubscribes mid-publish", () => {
		const bus = createEventBus<string>();
		const later = vi.fn();
		const unsubscribeFirst = bus.subscribe(() => unsubscribeFirst());
		bus.subscribe(later);

		bus.publish("event");

		expect(later).toHaveBeenCalledTimes(1);
	});

	// The mirror case: a listener added during a publish belongs to the *next*
	// event, not the one being delivered - otherwise a listener that subscribes
	// on every event would recurse without end.
	it("does not deliver the current event to a listener subscribed mid-publish", () => {
		const bus = createEventBus<string>();
		const latecomer = vi.fn();
		bus.subscribe(() => bus.subscribe(latecomer));

		bus.publish("first");
		expect(latecomer).not.toHaveBeenCalled();

		bus.publish("second");
		expect(latecomer).toHaveBeenCalledExactlyOnceWith("second");
	});

	it("registers the same listener only once", () => {
		const bus = createEventBus<string>();
		const listener = vi.fn();
		bus.subscribe(listener);
		bus.subscribe(listener);

		bus.publish("event");

		expect(listener).toHaveBeenCalledTimes(1);
	});

	it("publishes without an argument on a payload-less bus", () => {
		const bus = createEventBus();
		const listener = vi.fn();
		bus.subscribe(listener);

		bus.publish();

		expect(listener).toHaveBeenCalledTimes(1);
	});

	it("keeps two buses independent", () => {
		const first = createEventBus<string>();
		const second = createEventBus<string>();
		const listener = vi.fn();
		first.subscribe(listener);

		second.publish("event");

		expect(listener).not.toHaveBeenCalled();
	});
});
