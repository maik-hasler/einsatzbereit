import { defineConfig } from "@hey-api/openapi-ts";

/**
 * Generates the TypeScript API client from the OpenAPI document the backend
 * emits on every build (`backend/src/Api/Api.csproj`'s
 * `OpenApiGenerateDocumentsOnBuild`).
 *
 * NSwag still generates the C# client the integration tests use; only the
 * TypeScript half moved here. What that buys, in order of how much it matters:
 *
 *  1. Per-operation functions instead of one 7,500-line class.
 *  2. No `window` fallback in the transport, so the client runs anywhere a
 *     `fetch` does - which is what keeps a native shell a porting job rather
 *     than a rewrite.
 *  3. Options objects instead of positional parameters, which is what made
 *     `getVolunteerOpportunities(1, 10, undefined, undefined, ...)` readable.
 *
 * The `@tanstack/react-query` plugin is deliberately NOT enabled. Its
 * generated `queryOptions()` call the SDK directly, bypassing
 * `hooks/useApiClient.ts` - which is the seam every component test mocks, and
 * the only place the current bearer token is put in place. The hand-written
 * helpers in `src/lib/queryKeys.ts` cost a few lines and keep both.
 *
 * Run it with `pnpm client:generate`. `check:client-current` fails CI when the
 * committed output no longer matches the committed OpenAPI document.
 */
export default defineConfig({
	// Not the committed document directly: `scripts/normalize-openapi.js` runs
	// first and narrows the `["integer", "string"]` unions ASP.NET emits for
	// every numeric property (see that file for why they are there and why
	// narrowing them is correct). `.openapi/` is a build artefact.
	input: ".openapi/openapi-v1.json",
	output: {
		path: "src/client/generated",
		format: false,
		lint: false,
	},
	plugins: [
		{
			name: "@hey-api/client-fetch",
			// The client instance lives in src/client/api-instance.ts, which
			// configures the base URL, the auth/locale headers and the shared
			// error handling. Generating a second, self-configuring one would
			// give every call site two to choose from.
			runtimeConfigPath: "./src/client/api-instance.ts",
			// Errors are thrown, not returned beside the data: lib/apiError.ts
			// and every existing catch block read a rejected promise, and keeping
			// that contract is what makes this a client swap rather than a
			// rewrite of the app's error handling.
			throwOnError: true,
		},
		"@hey-api/sdk",
		{
			name: "@hey-api/typescript",
			enums: "javascript",
		},
		{
			// NSwag's fetch template parsed every `date-time` into a `Date`, and
			// the whole app is written against that: lib/format.ts, the calendar,
			// every slot comparison. Keeping the transformer on means this is a
			// client swap rather than a date-handling rewrite. `bigInt` stays off
			// because nothing in the schema is a long integer and BigInt values
			// would surprise arithmetic that has always been on numbers.
			name: "@hey-api/transformers",
			dates: true,
			bigInt: false,
		},
	],
});
