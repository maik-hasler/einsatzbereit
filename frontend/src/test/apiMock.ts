import { vi, type Mock } from "vitest";

export type ApiMock = Record<string, Mock> & { __reset: () => void };

export function createApiMock(): ApiMock {
	const methods = new Map<string, Mock>();

	const target = {
		__reset() {
			for (const fn of methods.values()) {
				fn.mockReset();
				fn.mockResolvedValue(undefined);
			}
		},
	};

	return new Proxy(target, {
		get(t, prop: string | symbol, receiver) {
			if (typeof prop !== "string" || prop in t) {
				return Reflect.get(t, prop, receiver);
			}
			let fn = methods.get(prop);
			if (!fn) {
				fn = vi.fn().mockResolvedValue(undefined);

				Object.defineProperty(fn, "name", { value: prop });
				methods.set(prop, fn);
			}
			return fn;
		},
	}) as ApiMock;
}
