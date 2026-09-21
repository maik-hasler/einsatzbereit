import * as sdk from "./generated/sdk.gen";

export * from "./generated/types.gen";

type Sdk = typeof sdk;

/**
 * `(options) => Promise<{ data, request, response }>` becomes
 * `(options) => Promise<data>`.
 *
 * Every generated operation is generic in `ThrowOnError`, and inferring over a
 * generic signature instantiates the parameter with its *constraint*
 * (`boolean`), not its default (`true`) - which makes the result the union of
 * both branches and leaves `data` possibly `undefined`. Picking the branch
 * that carries `request`/`response` is what selects the throwing one, and with
 * it the non-optional body this client always resolves to.
 */
type ThrowingResult<TResult> = Extract<
	TResult,
	{ request: Request; response: Response }
>;

type Unwrapped<TOperation> = TOperation extends (
	options: infer TOptions,
) => Promise<infer TResult>
	? ThrowingResult<TResult> extends { data: infer TData }
		? (options: TOptions) => Promise<TData>
		: TOperation
	: TOperation;

export type ApiClient = { [K in keyof Sdk]: Unwrapped<Sdk[K]> };

// Memoised so `api.getUserProfile` is the same function every time it is read.
// A proxy that builds a fresh closure per access would make every operation a
// new identity on every render, which is exactly the kind of thing that ends up
// in a dependency array and re-runs an effect forever.
const unwrapped = new Map<string, (options: unknown) => Promise<unknown>>();

/**
 * The API client every call site uses.
 *
 * The generated SDK resolves to `{ data, request, response }`. Only the body
 * is ever wanted here - across a hundred-odd call sites, not one reads the
 * `Request` or the `Response` back, because everything that looks at a
 * response already happens inside `api-instance.ts`'s `fetch` (auth headers,
 * toasts, the session-expiry signal). Unwrapping once, here, is what keeps
 * `await api.getUserProfile({})` meaning the profile.
 *
 * A failed call still rejects with the parsed `ProblemDetails` body, exactly
 * as the NSwag client did, so `lib/apiError.ts` and every `catch` in the app
 * read the same shape they always have.
 *
 * The cost is that this names the whole SDK namespace, so a bundler cannot
 * drop the operations a route does not call. That is a smaller price than it
 * looks: the generated operations are one-line wrappers over a shared client
 * (427 lines for 101 of them), and the per-operation request building that
 * actually took space in the previous generator is gone either way.
 */
export const api = new Proxy({} as ApiClient, {
	get(_target, operation: string) {
		const cached = unwrapped.get(operation);
		if (cached) return cached;

		const fn = (sdk as Record<string, unknown>)[operation];
		if (typeof fn !== "function") return undefined;

		const wrapper = async (options: unknown) => {
			const result = (await (fn as (o: unknown) => Promise<unknown>)(
				options,
			)) as { data: unknown };
			return result.data;
		};
		unwrapped.set(operation, wrapper);
		return wrapper;
	},
});
